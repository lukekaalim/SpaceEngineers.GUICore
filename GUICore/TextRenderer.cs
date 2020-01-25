using System;
using System.Collections.Generic;
using System.Linq;

namespace IngameScript
{
	class TextRenderer : GUICore.Renderer<TextNode>
	{
		Action<string> target;

		public TextRenderer(Component rootComponent, Worker renderWorker, Action<string> target) : base(rootComponent, renderWorker)
		{
			this.target = target;
		}

		public override void Draw(TextNode rootNode)
		{
			target(rootNode.Render());
		}
	}

	class TextNode
	{
		string text;

		public TextNode(string text)
		{
			this.text = text;
		}

		public string Render()
		{
			return text;
		}
	}
}
