using System;
using Cairo;
using GdkPixbuf;
using GObject;
using Gtk;
using Pinta.Core;
using Range = Gtk.Range;

namespace Pinta.Core;



public sealed class ColorPickerDialog : Gtk.Dialog
{

	private readonly PaletteManager palette;
	private readonly WorkspaceManager workspace;

	int rad = 200 / 2;
	private readonly Gtk.DrawingArea ColorCircle;
	private readonly Gtk.DrawingArea ColorCircleValue;
	private readonly Gtk.DrawingArea ColorCircleCursor;


	private readonly Gtk.Scale RBar;
	private readonly Gtk.Scale GBar;
	private readonly Gtk.Scale BBar;

	private readonly Gtk.Scale HueBar;
	private readonly Gtk.Scale SatBar;
	private readonly Gtk.Scale ValBar;

	private bool mouseDown = false;

	private HsvColor currentColor;
	private byte colorAlpha;


	public class LabelScale : Gtk.Box
	{
		public Gtk.Label label = new Gtk.Label();
		public Gtk.Scale slider = new Gtk.Scale ();

		public LabelScale (int upper, String text, double val, ref Gtk.Scale varOut)
		{
			label.SetLabel (text);
			label.WidthRequest = 50;
			slider.SetOrientation (Orientation.Horizontal);
			slider.SetAdjustment (Adjustment.New (0, 0, upper + 1, 1, 1, 1));
			//slider.DrawValue = true;
			slider.WidthRequest = 200;
			slider.Adjustment.Value = val;
			varOut = slider;
			this.Append (label);
			this.Append (slider);
		}
	}

	public ColorPickerDialog (ChromeManager chrome, WorkspaceManager workspace, PaletteManager palette)
	{
		this.palette = palette;
		const int spacing = 6;

		var c = palette.PrimaryColor;

		currentColor = new RgbColor ((byte)(c.R * 255.0), (byte)(c.G * 255.0), (byte)(c.B * 255.0)).ToHsv ();

		ColorCircle = new Gtk.DrawingArea ();
		ColorCircle.WidthRequest = rad * 2;
		ColorCircle.HeightRequest = rad * 2;
		ColorCircle.SetDrawFunc ((area, context, width, height) => Draw (context));

		ColorCircleValue = new Gtk.DrawingArea ();
		ColorCircleValue.WidthRequest = rad * 2;
		ColorCircleValue.HeightRequest = rad * 2;
		ColorCircleValue.SetDrawFunc ((area, context, width, height) => DrawValue (context));

		ColorCircleCursor = new Gtk.DrawingArea ();
		ColorCircleCursor.WidthRequest = rad * 2;
		ColorCircleCursor.HeightRequest = rad * 2;
		ColorCircleCursor.SetDrawFunc ((area, context, width, height) => DrawCursor (context));

		var click_gesture = Gtk.GestureClick.New ();
		click_gesture.SetButton (0); // Listen for all mouse buttons.
		click_gesture.OnPressed += (_, e) => {
			mouseDown = true;
		};
		click_gesture.OnReleased += (_, e) => {
			mouseDown = false;
		};
		AddController (click_gesture);

		var motion_controller = Gtk.EventControllerMotion.New ();
		motion_controller.OnMotion += (_, args) => {

			if (mouseDown)
				SetColorFromCircle(new PointD (args.X, args.Y));
		};
		AddController (motion_controller);


		var sliders = new Gtk.ListBox ();


		sliders.Append (new LabelScale (360, "Hue", currentColor.Hue, ref HueBar));
		HueBar.OnChangeValue += (_, args) => {
			HandleScaleChange (args, "hue");
			return false;
		};

		sliders.Append (new LabelScale (100, "Sat", currentColor.Saturation, ref SatBar));
		SatBar.OnChangeValue += (_, args) => {
			HandleScaleChange (args, "sat");
			return false;
		};

		sliders.Append (new LabelScale (100, "Value", currentColor.Value, ref ValBar));
		ValBar.OnChangeValue += (_, args) => {
			HandleScaleChange (args, "val");
			return false;
		};

		sliders.Append (new LabelScale (255, "Red", currentColor.Hue, ref RBar));
		RBar.OnChangeValue += (_, args) => {
			HandleScaleChange (args, "r");
			return false;
		};

		sliders.Append (new LabelScale (255, "Green", currentColor.Saturation, ref BBar));
		BBar.OnChangeValue += (_, args) => {
			HandleScaleChange (args, "g");
			return false;
		};

		sliders.Append (new LabelScale (255, "Blue", currentColor.Value, ref GBar));
		GBar.OnChangeValue += (_, args) => {
			HandleScaleChange (args, "b");
			return false;
		};


		var colorCircleOverlay = new Gtk.Overlay ();
		colorCircleOverlay.AddOverlay (ColorCircle);
		colorCircleOverlay.AddOverlay (ColorCircleValue);
		colorCircleOverlay.AddOverlay (ColorCircleCursor);
		colorCircleOverlay.HeightRequest = rad * 2;
		colorCircleOverlay.WidthRequest = rad * 2;


		//var h = new Gtk.Box ();


		//h.Append (ColorCircle);
		//h.Append (ColorCircleCursor);
		//h.Append (e);
		//h.Append (hueLS);

		Gtk.Box mainVbox = new () { Spacing = spacing };
		mainVbox.SetOrientation (Gtk.Orientation.Horizontal);

		mainVbox.Append (colorCircleOverlay);
		mainVbox.Append (sliders);

		// --- Initialization (Gtk.Window)

		Title = Translations.GetString ("test");
		TransientFor = chrome.MainWindow;
		Modal = true;
		IconName = Resources.Icons.ImageResizeCanvas;
		DefaultWidth = 300;
		DefaultHeight = 200;

		// --- Initialization (Gtk.Dialog)

		this.AddCancelOkButtons ();
		this.SetDefaultResponse (Gtk.ResponseType.Cancel);

		// --- Initialization

		var contentArea = this.GetContentAreaBox ();
		contentArea.SetAllMargins (12);
		contentArea.Append (mainVbox);
	}

	private void HandleScaleChange (Range.ChangeValueSignalArgs args, String scale)
	{
		if(scale == "r" || scale == "g" || scale == "b")
			currentColor = new RgbColor ((int)RBar.Adjustment.Value, (int)GBar.Adjustment.Value, (int)BBar.Adjustment.Value).ToHsv ();

		switch (scale) {
			case "hue":
				currentColor = new HsvColor ((int)args.Value, currentColor.Saturation, currentColor.Value);
				break;
			case "sat":
				currentColor = new HsvColor (currentColor.Hue, (int)args.Value, currentColor.Value);
				break;
			case "val":
				currentColor = new HsvColor (currentColor.Hue, currentColor.Saturation, (int)args.Value);
				ColorCircleValue.QueueDraw ();
				break;
		}

		SetColorFromHsv ();
	}

	private void DrawCursor (Context g)
	{
		var loc = HsvToLocation (currentColor, rad);
		loc = new PointD (loc.X + rad, loc.Y + rad);

		Console.WriteLine($"{loc.X}, {loc.Y}");

		g.DrawRectangle (new RectangleD (loc.X - 5, loc.Y - 5, 10, 10), new Color (0, 0, 0), 4);
		g.DrawRectangle (new RectangleD (loc.X - 5, loc.Y - 5, 10, 10), new Color (1, 1, 1), 1);
	}

	private void DrawValue (Context g)
	{
		var blackness = 1.0 - currentColor.Value / 100.0;

		g.FillEllipse (new RectangleD (0, 0, rad * 2, rad * 2), new Color (0, 0, 0, blackness));


	}

	// INCREDIBLY inefficient!!!
	private void Draw (Context g)
	{
		//g.DrawEllipse (new RectangleD (3, 3, 194, 194), PintaCore.Palette.SecondaryColor, 6);


		PointI center = new PointI (rad, rad);

		for (int y = 0; y <= rad * 2; y++) {
			for (int x = 0; x <= rad * 2; x++) {
				PointI pxl = new PointI (x, y);
				PointI vec = pxl - center;
				if (vec.Magnitude () <= rad) {
					var hue = (MathF.Atan2 (-vec.X, vec.Y) + MathF.PI) / (2f * MathF.PI) * 360f;

					var sat = Math.Min (vec.Magnitude () / rad, 1);

					var hsv = new HsvColor ((int) hue, (int) (sat * 100), 100);
					var rgb = hsv.ToRgb ();
					g.DrawRectangle (new RectangleD (x, y, 1, 1), new Color (rgb.Red / 255.0, rgb.Green / 255.0, rgb.Blue / 255.0), 2);
					g.Fill ();
				}
			}
		}
	}

	private PointD HsvToLocation (HsvColor color, int radius)
	{
		var rad = color.Hue * (Math.PI / 180.0) - (Math.PI / 2);
		var mult = radius / 100;
		var mag = color.Saturation * mult;
		var x = Math.Cos (rad) * mag;
		var y = Math.Sin (rad) * mag;
		return new PointD (x, y);
	}

	void SetColorFromCircle (PointD point)
	{
		//Console.WriteLine($"{point.X}");
		double x;
		double y;
		ColorCircle.TranslateCoordinates (this, 0.0, 0.0, out x, out y);

		PointI centre = new PointI (100, 100);
		PointI cursor = new PointI ((int)(point.X - x), (int)(point.Y - y));

		PointI vecCursor = cursor - centre;

		// Numbers from thin air!
		var hue = (MathF.Atan2 (-vecCursor.X, vecCursor.Y) + MathF.PI) / (2f * MathF.PI) * 360f;

		var sat = Math.Min(vecCursor.Magnitude () / 100.0, 1);

		Console.WriteLine(vecCursor.Magnitude ());

		currentColor = new HsvColor ((int)hue, (int)(sat * 100), currentColor.Value);
		SetColorFromHsv ();
	}

	bool SetColorFromHsv ()
	{
		HueBar.Adjustment.Value = currentColor.Hue;
		SatBar.Adjustment.Value = currentColor.Saturation;

		var rgb = currentColor.ToRgb ();

		RBar.Adjustment.Value = rgb.Red;
		GBar.Adjustment.Value = rgb.Green;
		BBar.Adjustment.Value = rgb.Blue;

		palette.PrimaryColor = new Color (rgb.Red / 255.0, rgb.Green / 255.0, rgb.Blue / 255.0);

		ColorCircleCursor.QueueDraw ();
		return true;
	}
}
