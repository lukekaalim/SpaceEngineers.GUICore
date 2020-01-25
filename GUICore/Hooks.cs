using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;

namespace IngameScript
{
	static partial class GUICore
	{
		public readonly struct Empty: IEquatable<Empty> {
			public static readonly Empty Instance = new Empty();

			public bool Equals(Empty other)
			{
				return true;
			}
		}

		public abstract class Hook
		{
			public struct State
			{
				static Stack<State> stateStack = new Stack<State>();

				public List<Hook> Hooks { get; private set; }
				int index;
				public Action MarkDirty { get; private set; }

				public State(List<Hook> hooks, Action markDirty)
				{
					Hooks = hooks;
					MarkDirty = markDirty;
					index = 0;
				}

				public Hook GetNextHook()
				{
					if (index < Hooks.Count)
						return Hooks[index++];

					return null;
				}

				public void RegisterHook(Hook hookToRegister)
				{
					index++;
					Hooks.Add(hookToRegister);
				}

				public static State Get()
				{
					return stateStack.Peek();
				}

				public static void Push(State newState)
				{
					stateStack.Push(newState);
				}

				public static State Pop()
				{
					return stateStack.Pop();
				}
			}

			protected Action MarkDirty { get; private set; }
			public static HookType GetNextHook<HookType, Argument>(Func<Argument, HookType> createHook, Argument argument) where HookType : Hook
			{
				Hook genericHook = State.Get().GetNextHook();
				HookType hook;
				if (genericHook == null)
				{
					hook = createHook(argument);
					hook.MarkDirty = State.Get().MarkDirty;
					State.Get().RegisterHook(hook);
				}
				else
				{
					hook = (HookType)genericHook;
				}
				return hook;
			}
			public virtual void Unmount() { }
		}
		public class UseEffect : Hook
		{
			protected bool initalized;
			Action deinitalizer;

			static UseEffect Create(Empty arg)
			{
				return new UseEffect();
			}

			public override void Unmount()
			{
				base.Unmount();
				deinitalizer();
				initalized = false;
			}

			public static void Hook(Func<Action> initalizer, List<IEqualityComparer> referenceList = null)
			{
				var hook = GetNextHook(Create, Empty.Instance);
				// always deinitalize
				// TODO: Add reference array
				if (hook.initalized)
				{
					hook.deinitalizer();
					hook.initalized = false;
				}

				if (!hook.initalized)
				{
					hook.deinitalizer = initalizer();
					hook.initalized = true;
				}
			}
		}
		public class UseState<T> : Hook
		{
			T state;

			static UseState<T> Create(T defaultValue)
			{
				return new UseState<T>()
				{
					state = defaultValue,
				};
			}

			public static Action<T> Hook(T defaultValue, out T state)
			{
				var hook = GetNextHook(Create, defaultValue);
				state = hook.state;
				return hook.SetState;
			}

			void SetState(T newState)
			{
				state = newState;
				MarkDirty();
			}
		}
	}
}
