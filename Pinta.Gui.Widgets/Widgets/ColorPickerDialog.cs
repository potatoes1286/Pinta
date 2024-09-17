using System;
using Cairo;
using GdkPixbuf;
using GObject;
using Gtk;
using Pinta.Core;
using Range = Gtk.Range;

namespace Pinta.Core;

public static class ColorExtensions
{
	// See RgbColor.ToHsv
	// h, s, v: 0 <= h <= 360; 0 <= s,v <= 1
	public static Tuple<double, double, double> Hsv (this Color c)
	{
		// In this function, R, G, and B values must be scaled
		// to be between 0 and 1.
		// HsvColor.Hue will be a value between 0 and 360, and
		// HsvColor.Saturation and value are between 0 and 1.

		double min = Math.Min (Math.Min (c.R, c.G), c.B);
		double max = Math.Max (Math.Max (c.R, c.G), c.B);

		double delta = max - min;

		double h;
		double s;
		double v;

		if (max == 0 || delta == 0) {
			// R, G, and B must be 0, or all the same.
			// In this case, S is 0, and H is undefined.
			// Using H = 0 is as good as any...
			s = 0;
			h = 0;
			v = max;
		} else {
			s = delta / max;
			if (c.R == max) {
				// Between Yellow and Magenta
				h = (c.G - c.B) / delta;
			} else if (c.G == max) {
				// Between Cyan and Yellow
				h = 2 + (c.B - c.R) / delta;
			} else {
				// Between Magenta and Cyan
				h = 4 + (c.R - c.G) / delta;
			}
			v = max;
		}
		// Scale h to be between 0 and 360.
		// This may require adding 360, if the value
		// is negative.
		h *= 60;

		if (h < 0) {
			h += 360;
		}

		// Scale to the requirements of this
		// application. All values are between 0 and 255.
		return new (h,s,v);
	}

	public static double Hue (this Color c)
	{
		return c.Hsv ().Item1;
	}

	public static double Sat (this Color c)
	{
		return c.Hsv ().Item2;
	}

	public static double Val (this Color c)
	{
		return c.Hsv ().Item3;
	}

	public static void SetRgba (this ref Color c, double? r = null, double? g = null, double? b = null, double? a = null)
	{
		c = new Color (r ?? c.R, g ?? c.G, b ?? c.B, a ?? c.A);
	}

	// See HsvColor.ToRgb
	public static void SetHsv (this ref Color c, double? hue = null, double? saturation = null, double? value = null)
	{
		var hsv = c.Hsv ();

		double h = hue ?? hsv.Item1;
		double s = saturation ?? hsv.Item2;
		double v = value ?? hsv.Item3;

		// HsvColor contains values scaled as in the color wheel.
		// Scale Hue to be between 0 and 360. Saturation
		// and value scale to be between 0 and 1.
		h %= 360.0;

		double r = 0;
		double g = 0;
		double b = 0;

		if (s == 0) {
			// If s is 0, all colors are the same.
			// This is some flavor of gray.
			r = v;
			g = v;
			b = v;
		} else {
			// The color wheel consists of 6 sectors.
			// Figure out which sector you're in.
			double sectorPos = h / 60;
			int sectorNumber = (int) (Math.Floor (sectorPos));

			// get the fractional part of the sector.
			// That is, how many degrees into the sector
			// are you?
			double fractionalSector = sectorPos - sectorNumber;

			// Calculate values for the three axes
			// of the color.
			double p = v * (1 - s);
			double q = v * (1 - (s * fractionalSector));
			double t = v * (1 - (s * (1 - fractionalSector)));

			// Assign the fractional colors to r, g, and b
			// based on the sector the angle is in.
			switch (sectorNumber) {
				case 0:
					r = v;
					g = t;
					b = p;
					break;

				case 1:
					r = q;
					g = v;
					b = p;
					break;

				case 2:
					r = p;
					g = v;
					b = t;
					break;

				case 3:
					r = p;
					g = q;
					b = v;
					break;

				case 4:
					r = t;
					g = p;
					b = v;
					break;

				case 5:
					r = v;
					g = p;
					b = q;
					break;
			}
		}
		// return an RgbColor structure, with values scaled
		// to be between 0 and 255.
		c = new Color (r, g, b, c.A);
	}
}

public sealed class ColorPickerDialog : Gtk.Dialog
{

	private readonly PaletteManager palette;
	private readonly WorkspaceManager workspace;

	int ColorCircleRadius = 200 / 2;
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


	private Color currentColor;





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

		currentColor = palette.PrimaryColor;

		ColorCircle = new Gtk.DrawingArea ();
		ColorCircle.WidthRequest = ColorCircleRadius * 2;
		ColorCircle.HeightRequest = ColorCircleRadius * 2;
		ColorCircle.SetDrawFunc ((area, context, width, height) => Draw (context));

		ColorCircleValue = new Gtk.DrawingArea ();
		ColorCircleValue.WidthRequest = ColorCircleRadius * 2;
		ColorCircleValue.HeightRequest = ColorCircleRadius * 2;
		ColorCircleValue.SetDrawFunc ((area, context, width, height) => DrawValue (context));

		ColorCircleCursor = new Gtk.DrawingArea ();
		ColorCircleCursor.WidthRequest = ColorCircleRadius * 2;
		ColorCircleCursor.HeightRequest = ColorCircleRadius * 2;
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


		sliders.Append (new LabelScale (360, "Hue", currentColor.Hue(), ref HueBar));
		HueBar.OnChangeValue += (_, args) => {
			HandleScaleChange (args, "hue");
			return false;
		};

		sliders.Append (new LabelScale (100, "Sat", currentColor.Sat() * 100, ref SatBar));
		SatBar.OnChangeValue += (_, args) => {
			HandleScaleChange (args, "sat");
			return false;
		};

		sliders.Append (new LabelScale (100, "Value", currentColor.Val() * 100, ref ValBar));
		ValBar.OnChangeValue += (_, args) => {
			HandleScaleChange (args, "val");
			return false;
		};

		sliders.Append (new LabelScale (255, "Red", currentColor.Hue(), ref RBar));
		RBar.OnChangeValue += (_, args) => {
			HandleScaleChange (args, "r");
			return false;
		};

		sliders.Append (new LabelScale (255, "Green", currentColor.Sat () * 100, ref BBar));
		BBar.OnChangeValue += (_, args) => {
			HandleScaleChange (args, "g");
			return false;
		};

		sliders.Append (new LabelScale (255, "Blue", currentColor.Val() * 100, ref GBar));
		GBar.OnChangeValue += (_, args) => {
			HandleScaleChange (args, "b");
			return false;
		};


		var colorCircleOverlay = new Gtk.Overlay ();
		colorCircleOverlay.AddOverlay (ColorCircle);
		colorCircleOverlay.AddOverlay (ColorCircleValue);
		colorCircleOverlay.AddOverlay (ColorCircleCursor);
		colorCircleOverlay.HeightRequest = ColorCircleRadius * 2;
		colorCircleOverlay.WidthRequest = ColorCircleRadius * 2;


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
		if (scale == "r" || scale == "g" || scale == "b")
			//currentColor = new Color (RBar.Adjustment.Value / 255.0, GBar.Adjustment.Value / 255.0, BBar.Adjustment.Value / 255.0);
			currentColor.SetRgba (RBar.Adjustment.Value / 255.0, GBar.Adjustment.Value / 255.0, BBar.Adjustment.Value / 255.0);



		switch (scale) {
			case "hue":
				currentColor.SetHsv (hue: args.Value);
				break;
			case "sat":
				currentColor.SetHsv (saturation: args.Value / 100.0);
				break;
			case "val":
				currentColor.SetHsv (value: args.Value / 100.0);
				ColorCircleValue.QueueDraw ();
				break;
		}

		SetColorFromHsv ();
	}

	private void DrawCursor (Context g)
	{
		var loc = HsvToLocation (currentColor.Hsv (), ColorCircleRadius);
		loc = new PointD (loc.X + ColorCircleRadius, loc.Y + ColorCircleRadius);

		Console.WriteLine($"{loc.X}, {loc.Y}");

		g.DrawRectangle (new RectangleD (loc.X - 5, loc.Y - 5, 10, 10), new Color (0, 0, 0), 4);
		g.DrawRectangle (new RectangleD (loc.X - 5, loc.Y - 5, 10, 10), new Color (1, 1, 1), 1);
	}

	private void DrawValue (Context g)
	{
		var blackness = 1.0 - currentColor.Val ();

		g.FillEllipse (new RectangleD (0, 0, ColorCircleRadius * 2, ColorCircleRadius * 2), new Color (0, 0, 0, blackness));


	}

	// INCREDIBLY inefficient!!!
	private void Draw (Context g)
	{
		//g.DrawEllipse (new RectangleD (3, 3, 194, 194), PintaCore.Palette.SecondaryColor, 6);


		PointI center = new PointI (ColorCircleRadius, ColorCircleRadius);

		for (int y = 0; y <= ColorCircleRadius * 2; y++) {
			for (int x = 0; x <= ColorCircleRadius * 2; x++) {
				PointI pxl = new PointI (x, y);
				PointI vec = pxl - center;
				if (vec.Magnitude () <= ColorCircleRadius) {
					var hue = (MathF.Atan2 (-vec.X, vec.Y) + MathF.PI) / (2f * MathF.PI) * 360f;

					var sat = Math.Min (vec.Magnitude () / ColorCircleRadius, 1);

					var hsv = new HsvColor ((int) hue, (int) (sat * 100), 100);
					var rgb = hsv.ToRgb ();
					g.DrawRectangle (new RectangleD (x, y, 1, 1), new Color (rgb.Red / 255.0, rgb.Green / 255.0, rgb.Blue / 255.0), 2);
					g.Fill ();
				}
			}
		}
	}

	private PointD HsvToLocation (Tuple<double, double, double> hsv, int radius)
	{
		var rad = hsv.Item1 * (Math.PI / 180.0) - (Math.PI / 2);
		var mult = radius;
		var mag = hsv.Item2 * mult;
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

		currentColor.SetHsv (hue: hue, saturation: sat);
		SetColorFromHsv ();
	}

	bool SetColorFromHsv ()
	{
		HueBar.Adjustment.Value = currentColor.Hue ();
		SatBar.Adjustment.Value = currentColor.Sat () * 100;

		RBar.Adjustment.Value = currentColor.R * 255.0;
		GBar.Adjustment.Value = currentColor.G * 255.0;
		BBar.Adjustment.Value = currentColor.B * 255.0;

		palette.PrimaryColor = currentColor;

		ColorCircleCursor.QueueDraw ();
		return true;
	}
}
