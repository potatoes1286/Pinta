//
// RectangleSelectTool.cs
//
// Author:
//       Jonathan Pobst <monkey@jpobst.com>
//
// Copyright (c) 2010 Jonathan Pobst
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using Cairo;
using Pinta.Core;

namespace Pinta.Tools;

public sealed class RectangleSelectTool : SelectTool
{
	public RectangleSelectTool (IServiceProvider services) : base (services) { }

	public override string Name => Translations.GetString ("Rectangle Select");
	public override string Icon => Pinta.Resources.Icons.ToolSelectRectangle;
	public override string StatusBarText => Translations.GetString (
		"Click and drag to draw a rectangular selection." +
		"\nHold Shift to constrain to a square.");
	public override Gdk.Cursor DefaultCursor => Gdk.Cursor.NewFromTexture (Resources.GetIcon ("Cursor.RectangleSelect.png"), 9, 18, null);
	public override int Priority => 13;

	protected override void DrawShape (Document document, RectangleD r, Layer l)
	{
		document.Selection.CreateRectangleSelection (r);
	}

	#region ToolBar

	private Gtk.Button? select_layer_content_button = null;
	private Gtk.Button? select_image_content_button = null;

	protected override void OnBuildToolBar (Gtk.Box tb)
	{
		base.OnBuildToolBar (tb);

		if (select_layer_content_button == null) {
			select_layer_content_button = new Gtk.Button {
				TooltipText = Translations.GetString ("Select Layer Content"),
				IconName = Pinta.Resources.Icons.ToolMoveSelection
			};
			select_layer_content_button.OnClicked += HandleSelectLayerContentPressed;
		}
		tb.Append (select_layer_content_button);

		if (select_image_content_button == null) {
			select_image_content_button = new Gtk.Button {
				TooltipText = Translations.GetString ("Select Image Content"),
				IconName = Pinta.Resources.Icons.ToolMoveSelection
			};
			select_image_content_button.OnClicked += HandleSelectImageContentPressed;
		}
		tb.Append (select_image_content_button);
	}

	#endregion

	#region Toolbar Handlers

	private void HandleSelectLayerContentPressed (object? sender, EventArgs e)
	{
		Document doc = PintaCore.Workspace.ActiveDocument;
		var image = doc.Layers.CurrentUserLayer.Surface;

		SelectContent (doc, image);
	}

	private void HandleSelectImageContentPressed (object? sender, EventArgs e)
	{
		Document doc = PintaCore.Workspace.ActiveDocument;
		var image = doc.GetFlattenedImage ();

		SelectContent (doc, image);
	}

	private void SelectContent (Document doc, ImageSurface image)
	{
		RectangleI rect = image.GetBounds ();
		Color border_color = image.GetColorBgra (PointI.Zero).ToCairoColor ();

		// Top down.
		for (int y = 0; y < image.Height; ++y) {
			if (!ImageActions.IsConstantRow (image, border_color, y))
				break;

			rect = rect with { Y = rect.Y + 1, Height = rect.Height - 1 };
		}

		// Bottom up.
		for (int y = rect.Bottom; y >= rect.Top; --y) {
			if (!ImageActions.IsConstantRow (image, border_color, y))
				break;

			rect = rect with { Height = rect.Height - 1 };
		}

		// Left side.
		for (int x = 0; x < image.Width; ++x) {
			if (!ImageActions.IsConstantColumn (image, border_color, rect, x))
				break;

			rect = rect with { X = rect.X + 1, Width = rect.Width - 1 };
		}

		// Right side.
		for (int x = rect.Right; x >= rect.Left; --x) {
			if (!ImageActions.IsConstantColumn (image, border_color, rect, x))
				break;

			rect = rect with { Width = rect.Width - 1 };
		}

		if (rect.Width == 0 || rect.Height == 0)
			rect = new RectangleI (0, 0, image.Width, image.Height);

		RectToSelection (doc, rect);
	}

	#endregion
}
