using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using IngameScript;

namespace DGUI_Tests
{
	[TestClass]
	public class UnitTest1
	{
		[TestMethod]
		public void TestMethod1()
		{
			var worker = new Worker();
			TextRenderer.Component<bool> nodeComponent = (isInitalRender) => new TextNode($"Hello, this is the inital render = {isInitalRender}");
			TextRenderer.Component nestedComponent = () => TextRenderer.CreateElement(nodeComponent, true, "base");

			string target = "";
			var renderer = new TextRenderer(nestedComponent, worker, content => target = content);

			worker.Work();

			Assert.AreEqual("Hello, this is the inital render = True", target);
		}

		[TestMethod]
		public void RerenderTest()
		{
			var worker = new Worker();

			Action<int> setFavoriteNumber = null;

			TextRenderer.Component nestedComponent = () =>
			{
				int favoriteNumber;
				setFavoriteNumber = GUICore.UseState<int>.Hook(10, out favoriteNumber);
				return new TextNode($"My favorite number is {favoriteNumber}");
			};

			string target = "";
			var renderer = new TextRenderer(nestedComponent, worker, content => target = content);

			worker.Work();

			Assert.AreEqual("My favorite number is 10", target);

			setFavoriteNumber(20);

			Assert.AreEqual("My favorite number is 10", target);

			worker.Work();

			Assert.AreEqual("My favorite number is 20", target);
		}

		[TestMethod]
		public void NestedRerenderTest()
		{
			var worker = new Worker();

			int component1RenderCount = 0;
			int component2RenderCount = 0;
			Action<int> setFavoriteNumber = null;

			TextRenderer.Component<GUICore.Empty> component1 = (empty) =>
			{
				component1RenderCount++;
				int favoriteNumber;
				setFavoriteNumber = GUICore.UseState<int>.Hook(10, out favoriteNumber);
				return new TextNode($"My favorite number is {favoriteNumber}");
			};

			TextRenderer.Component component2 = () =>
			{
				component2RenderCount++;
				return TextRenderer.CreateElement(component1, "1");
			};

			string target = "";
			var renderer = new TextRenderer(component2, worker, content => target = content);

			worker.Work();

			Assert.AreEqual(component1RenderCount, 1);
			Assert.AreEqual(component2RenderCount, 1);

			setFavoriteNumber(20);
			worker.Work();

			Assert.AreEqual(component1RenderCount, 2);
			Assert.AreEqual(component2RenderCount, 1);
		}
	}
}
