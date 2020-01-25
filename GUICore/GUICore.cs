using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;

namespace IngameScript
{
	static partial class GUICore
	{
		public class Element<Node>
		{
			public object LastProps { get; set; }
			public object LastComponent { get; set; }
			public Node LastResult { get; set; }
			public List<Hook> Hooks { get; set; } = new List<Hook>();
			public Dictionary<string, Element<Node>> LastChildrenByKeys { get; private set; } = new Dictionary<string, Element<Node>>();
			public HashSet<string> CurrentChildrenKeys { get; private set; } = new HashSet<string>();
			public Func<Node> RerunLastRender { get; set; }
			public bool Mounted { get; set; }
		}

		public abstract class Renderer<Node>
		{
			readonly Worker worker;

			public delegate Node Component<Props>(Props props);
			public delegate Node Component();

			public static Renderer<Node> activeRenderer = null;

			Stack<Element<Node>> renderingElements = new Stack<Element<Node>>();

			static void UnmountElement(Element<Node> node)
			{
				foreach (var hook in node.Hooks)
					hook.Unmount();

				node.Hooks.Clear();
				node.LastChildrenByKeys.Clear();
				node.CurrentChildrenKeys.Clear();
				node.LastProps = null;
				node.LastComponent = null;
				node.Mounted = false;
			}

			enum UpdateType
			{
				None,
				NewProps,
				NewComponent
			}
			static UpdateType GetComponentUpdateType<Props>(
				Component<Props> component,
				Props props,
				Element<Node> lastElement
			) where Props : IEquatable<Props>
			{
				if ((object)component == lastElement.LastComponent)
				{
					if (props.Equals((Props)lastElement.LastProps))
					{
						return UpdateType.None;
					}
					else
					{
						return UpdateType.NewProps;
					}
				}
				else
				{
					return UpdateType.NewComponent;
				}
			}
			public static Node CreateElement(
				Component<Empty> component,
				string key
			)
			{
				return CreateElement(component, Empty.Instance, key);
			}

				public static Node CreateElement<Props>(
				Component<Props> component,
				Props props,
				string key
			) where Props : IEquatable<Props>
			{
				// fetch ourselves
				Element<Node> parentElement = activeRenderer.renderingElements.Peek();
				Element<Node> currentElement = activeRenderer.GetElementByKey(key);
				// Mark ourselves as rendering to our parent
				parentElement.CurrentChildrenKeys.Add(key);
				parentElement.LastChildrenByKeys[key] = currentElement;
				var updateType = GetComponentUpdateType(component, props, currentElement);

				if (updateType == UpdateType.None)
					return currentElement.LastResult;

				if (updateType == UpdateType.NewComponent)
					UnmountElement(currentElement);

				// Setup some state for rendering
				currentElement.CurrentChildrenKeys.Clear();
				activeRenderer.renderingElements.Push(currentElement);
				Hook.State.Push(new Hook.State(currentElement.Hooks, () => activeRenderer.MarkNodeDirty(currentElement)));

				// Render our new component & save the results
				currentElement.LastResult = component(props);
				currentElement.LastComponent = component;
				currentElement.Mounted = true;
				currentElement.LastProps = props;
				currentElement.Hooks = Hook.State.Pop().Hooks;
				currentElement.RerunLastRender = () => component(props);

				// Unmount all elements that were not called this render (compared to last render)
				foreach (var childKey in currentElement.LastChildrenByKeys.Keys)
				{
					if (!currentElement.CurrentChildrenKeys.Contains(childKey))
					{
						UnmountElement(currentElement.LastChildrenByKeys[childKey]);
						currentElement.LastChildrenByKeys.Remove(childKey);
					}
				}

				activeRenderer.renderingElements.Pop();
				return currentElement.LastResult;
			}

			Queue<Element<Node>> dirtyNodes = new Queue<Element<Node>>();

			public void MarkNodeDirty(Element<Node> dirtyNode)
			{
				if (dirtyNodes.Count == 0)
					worker.Register(RedrawChangedNodes);
				dirtyNodes.Enqueue(dirtyNode);
			}

			public Element<Node> GetElementByKey(string key)
			{
				Element<Node> previousElement;
				if (renderingElements.Peek().LastChildrenByKeys.TryGetValue(key, out previousElement))
				{
					return previousElement;
				} else
				{
					return new Element<Node>();
				}
			}

			Element<Node> baseElement;

			public Renderer(Component rootComponent, Worker worker)
			{
				activeRenderer = this;
				this.worker = worker;
				this.baseElement = new Element<Node>();
				this.baseElement.RerunLastRender = () => rootComponent();
				this.baseElement.Mounted = true;
				MarkNodeDirty(this.baseElement);
			}

			protected virtual void RedrawChangedNodes()
			{
				Element<Node> dirtyElement;
				while (dirtyNodes.TryDequeue(out dirtyElement))
				{
					Element<Node> currentElement = dirtyElement;
					// don't redraw unmounted elements
					if (!currentElement.Mounted)
						continue;

					// Setup some state for rendering
					currentElement.CurrentChildrenKeys.Clear();
					renderingElements.Push(currentElement);
					Hook.State.Push(new Hook.State(currentElement.Hooks, () => MarkNodeDirty(currentElement)));

					// Render our new component
					currentElement.LastResult = currentElement.RerunLastRender();
					currentElement.Hooks = Hook.State.Pop().Hooks;

					// Unmount all elements that were not called this render (compared to last render)
					foreach (var childKey in currentElement.LastChildrenByKeys.Keys)
					{
						if (!currentElement.CurrentChildrenKeys.Contains(childKey))
						{
							UnmountElement(currentElement.LastChildrenByKeys[childKey]);
							currentElement.LastChildrenByKeys.Remove(childKey);
						}
					}
					renderingElements.Pop();
				}

				Draw(baseElement.LastResult);
			}

			public abstract void Draw(Node rootNode);
		}
	}
}
