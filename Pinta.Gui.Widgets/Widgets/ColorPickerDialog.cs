using System;
using System.Globalization;
using Adw;
using Cairo;
using GdkPixbuf;
using GObject;
using Gtk;
using Pango;
using Pinta.Core;
using Color = Cairo.Color;
using Context = Cairo.Context;
using Pattern = Cairo.Internal.Pattern;
using Range = Gtk.Range;

namespace Pinta.Core;

public static class ColorExtensions
{

	public static String ToHex (this Color c)
	{
		int r = Convert.ToInt32(c.R * 255.0);
		int g = Convert.ToInt32(c.G * 255.0);
		int b = Convert.ToInt32(c.B * 255.0);
		int a = Convert.ToInt32(c.A * 255.0);

		return $"{r:X2}{g:X2}{b:X2}{a:X2}";
	}

	public static bool FromHex (this ref Color c, String hex)
	{
		if (hex.Length != 6 && hex.Length != 8)
			return false;
		try {

			int r = int.Parse (hex.Substring (0, 2), NumberStyles.HexNumber);
			int g = int.Parse (hex.Substring (2, 2), NumberStyles.HexNumber);
			int b = int.Parse (hex.Substring (4, 2), NumberStyles.HexNumber);
			int a = 255;
			if (hex.Length > 6)
				a = int.Parse (hex.Substring (5, 2), NumberStyles.HexNumber);

			Console.WriteLine($"{r}, {g}, {b}, {a}");

			c.SetRgba (r / 255.0, g / 255.0, b / 255.0, a / 255.0);
		} catch {
			Console.WriteLine("Fuck!");
			return false;
		}
		return true;
	}

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

		// Stupid hack!
		// If v is set to 0, it forces sat and hue to 0 as well
		if (v == 0)
			v = 0.005;

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


	private int ColorDisplaySize = 50;
	private int ColorDisplayBorderSize = 3;
	private readonly Gtk.DrawingArea ColorDisplayPrimary;
	private readonly Gtk.DrawingArea ColorDisplaySecondary;

	private int ColorCircleRadius = 200 / 2;
	private int CirclePadding = 10;
	private readonly Gtk.DrawingArea ColorCircle;
	private readonly Gtk.DrawingArea ColorCircleValue;
	private readonly Gtk.DrawingArea ColorCircleCursor;


	private readonly LabelScale RWidget;
	private readonly LabelScale GWidget;
	private readonly LabelScale BWidget;
	private readonly LabelScale AWidget;

	private readonly LabelScale HueWidget;
	private readonly LabelScale SatWidget;
	private readonly LabelScale ValWidget;

	private readonly Entry HexEntry;

	private bool mouseDown = false;

	private bool showValue = true;


	private Color currentColor;

	public Color primaryColor;
	public Color secondaryColor;

	private bool editingPrimaryColor = true;



	public class LabelScale : Gtk.Box
	{
		private readonly Gtk.Window topWindow;

		public Gtk.Label label = new Gtk.Label ();
		public Gtk.Scale slider = new Gtk.Scale ();
		public Gtk.Entry input = new Gtk.Entry ();

		public int maxVal;

		public class OnChangeValArgs : EventArgs
		{
			public string senderName;
			public double value;
		}


		private bool entryBeingEdited = false;
		public LabelScale (int upper, String text, double val, Gtk.Window topWindow)
		{
			maxVal = upper;
			this.topWindow = topWindow;
			label.SetLabel (text);
			label.WidthRequest = 50;
			slider.SetOrientation (Orientation.Horizontal);
			slider.SetAdjustment (Adjustment.New (0, 0, maxVal + 1, 1, 1, 1));
			//slider.DrawValue = true;
			slider.WidthRequest = 200;
			slider.SetValue (val);

			input.WidthRequest = 50;
			input.SetText (Convert.ToInt32(val).ToString());
			this.Append (label);
			this.Append (slider);
			this.Append (input);

			slider.OnChangeValue += (sender, args) => {
				if (suppressEvent > 0) {
					suppressEvent--;
					return false;
				}
				var e = new OnChangeValArgs ();


				e.senderName = label.GetLabel ();
				e.value = slider.GetValue ();
				input.SetText (e.value.ToString(CultureInfo.InvariantCulture));
				OnValueChange?.Invoke (this, e);
				return false;
			};

			input.OnChanged ((o, e) => {
				if (suppressEvent > 0) {
					suppressEvent--;
					return;
				}
				var t = o.GetText ();
				double val;
				var success = double.TryParse (t, CultureInfo.InvariantCulture, out val);

				if (val > maxVal) {
					val = maxVal;
					input.SetText (Convert.ToInt32(val).ToString());
				}


				if (success) {
					var e2 = new OnChangeValArgs ();
					e2.senderName = label.GetLabel ();
					e2.value = val;
					OnValueChange?.Invoke (this, e2);
				}
			});

		}

		public event EventHandler<OnChangeValArgs> OnValueChange;

		private int suppressEvent = 0;
		public void SetValue (double val)
		{
			suppressEvent = 1;
			slider.SetValue (val);
			// Make sure we do not set the text if we are editing it right now
			if (topWindow.GetFocus ()?.Parent != input) {
				suppressEvent++;
				input.SetText (Convert.ToInt32(val).ToString());
			}
		}

	}

	public ColorPickerDialog (ChromeManager chrome, WorkspaceManager workspace, PaletteManager palette, bool isPrimaryColor)
	{
		editingPrimaryColor = isPrimaryColor;

		this.palette = palette;
		const int spacing = 6;

		if(editingPrimaryColor)
			currentColor = palette.PrimaryColor;
		else
			currentColor = palette.SecondaryColor;
		primaryColor = palette.PrimaryColor;
		secondaryColor = palette.SecondaryColor;


		var topBox = new Gtk.Box {Spacing = spacing};

		#region Color Display

		var colorDisplayArea = new Gtk.ListBox ();

		ColorDisplayPrimary = new Gtk.DrawingArea ();
		ColorDisplayPrimary.SetSizeRequest (ColorDisplaySize, ColorDisplaySize);
		ColorDisplayPrimary.SetDrawFunc ((area, context, width, height) => DrawColorDisplay (context, primaryColor));

		colorDisplayArea.Append (ColorDisplayPrimary);

		ColorDisplaySecondary = new Gtk.DrawingArea ();
		ColorDisplaySecondary.SetSizeRequest (ColorDisplaySize, ColorDisplaySize);
		ColorDisplaySecondary.SetDrawFunc ((area, context, width, height) => DrawColorDisplay (context, secondaryColor));

		colorDisplayArea.Append (ColorDisplaySecondary);

		if(editingPrimaryColor)
			colorDisplayArea.SelectRow (colorDisplayArea.GetRowAtIndex (0));
		else
			colorDisplayArea.SelectRow (colorDisplayArea.GetRowAtIndex (1));
		colorDisplayArea.SetSelectionMode (SelectionMode.Single);

		colorDisplayArea.OnRowSelected += HandleSelectPrimSec;

		#endregion

		#region Color Circle

		var colorCircleBox = new Gtk.Box { Spacing = spacing };
		colorCircleBox.SetOrientation (Orientation.Vertical);

		var DrawingAreaSize = (ColorCircleRadius + CirclePadding) * 2;

		ColorCircle = new Gtk.DrawingArea ();
		ColorCircle.WidthRequest = DrawingAreaSize;
		ColorCircle.HeightRequest = DrawingAreaSize;
		ColorCircle.SetDrawFunc ((area, context, width, height) => DrawColorCircle (context));

		ColorCircleValue = new Gtk.DrawingArea ();
		ColorCircleValue.WidthRequest = DrawingAreaSize;
		ColorCircleValue.HeightRequest = DrawingAreaSize;
		ColorCircleValue.SetDrawFunc ((area, context, width, height) => DrawValue (context));

		ColorCircleCursor = new Gtk.DrawingArea ();
		ColorCircleCursor.WidthRequest = DrawingAreaSize;
		ColorCircleCursor.HeightRequest = DrawingAreaSize;
		ColorCircleCursor.SetDrawFunc ((area, context, width, height) => DrawCursor (context));

		var colorCircleOverlay = new Gtk.Overlay ();
		colorCircleOverlay.AddOverlay (ColorCircle);
		colorCircleOverlay.AddOverlay (ColorCircleValue);
		colorCircleOverlay.AddOverlay (ColorCircleCursor);
		colorCircleOverlay.HeightRequest = DrawingAreaSize;
		colorCircleOverlay.WidthRequest = DrawingAreaSize;

		colorCircleBox.Append (colorCircleOverlay);

		var colorCircleValueToggleBox = new Gtk.Box ();

		// TODO: Remember setting!
		var colorCircleValueToggle = new Gtk.CheckButton ();
		colorCircleValueToggle.Active = showValue;
		colorCircleValueToggle.OnToggled += (o, e) => {
			showValue = !showValue;
			colorCircleValueToggle.Active = showValue;
			ColorCircleValue.QueueDraw ();
		};

		colorCircleValueToggleBox.Append (colorCircleValueToggle);

		var colorCircleValueToggleLabel = new Gtk.Label { Label_ = "Show Value" };
		colorCircleValueToggleBox.Append (colorCircleValueToggleLabel);

		colorCircleBox.Append (colorCircleValueToggleBox);

		#endregion

		#region Mouse Handler

		var click_gesture = Gtk.GestureClick.New ();
		click_gesture.SetButton (0); // Listen for all mouse buttons.
		click_gesture.OnPressed += (_, e) => {
			double x;
			double y;
			ColorCircle.TranslateCoordinates (this, CirclePadding, CirclePadding, out x, out y);

			PointI cursor = new PointI ((int)(e.X - x), (int)(e.Y - y));

			if (cursor.X < 0 || cursor.X > (ColorCircleRadius + CirclePadding) * 2 || cursor.Y < 0 || cursor.Y > (ColorCircleRadius + CirclePadding) * 2)
				return;
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

		#endregion

		#region SliderAndHex

		var sliders = new Gtk.Box {Spacing = spacing};
		sliders.SetOrientation (Orientation.Vertical);

		var hexBox = new Gtk.Box { Spacing = spacing };

		hexBox.Append (new Label { Label_ = "Hex", WidthRequest = 50});
		HexEntry = new Entry { Text_ = currentColor.ToHex ()};
		HexEntry.OnChanged ((o, e) => {
			if (GetFocus ()?.Parent == HexEntry) {
				Console.WriteLine("HEA");
				currentColor.FromHex (HexEntry.GetText ());
				ColorCircleValue.QueueDraw ();
				SetColorFromHsv ();
			}
		});

		hexBox.Append (HexEntry);


		sliders.Append (hexBox);


		HueWidget = new LabelScale (360, "Hue", currentColor.Hue (), this);
		HueWidget.OnValueChange += (sender, args) => {
			currentColor.SetHsv (hue: args.value);
			SetColorFromHsv ();
		};
		sliders.Append (HueWidget);

		SatWidget = new LabelScale (100, "Sat", currentColor.Sat () * 100.0, this);
		SatWidget.OnValueChange += (sender, args) => {
			currentColor.SetHsv (saturation: args.value / 100.0);
			SetColorFromHsv ();
		};
		sliders.Append (SatWidget);


		ValWidget = new LabelScale (100, "Value", currentColor.Val () * 100.0, this);
		ValWidget.OnValueChange += (sender, args) => {
			currentColor.SetHsv (value: args.value / 100.0);
			SetColorFromHsv ();
			ColorCircleValue.QueueDraw ();
		};
		sliders.Append (ValWidget);

		sliders.Append (new Gtk.Separator());

		RWidget = new LabelScale (255, "Red", currentColor.R * 255.0, this);
		RWidget.OnValueChange += (sender, args) => {
			currentColor.SetRgba (r: args.value / 255.0);
			ColorCircleValue.QueueDraw ();
			SetColorFromHsv ();
		};
		sliders.Append (RWidget);
		GWidget = new LabelScale (255, "Green", currentColor.G * 255.0, this);
		GWidget.OnValueChange += (sender, args) => {
			currentColor.SetRgba (g: args.value / 255.0);
			ColorCircleValue.QueueDraw ();
			SetColorFromHsv ();
		};
		sliders.Append (GWidget);
		BWidget = new LabelScale (255, "Blue", currentColor.B * 255.0, this);
		BWidget.OnValueChange += (sender, args) => {
			currentColor.SetRgba (b: args.value / 255.0);
			ColorCircleValue.QueueDraw ();
			SetColorFromHsv ();
		};
		sliders.Append (BWidget);
		AWidget = new LabelScale (255, "Alpha", currentColor.A * 255.0, this);
		AWidget.OnValueChange += (sender, args) => {
			currentColor.SetRgba (a: args.value / 255.0);
			ColorCircleValue.QueueDraw ();
			SetColorFromHsv ();
		};
		sliders.Append (AWidget);

		#endregion

		Gtk.Box mainVbox = new () { Spacing = spacing };
		mainVbox.SetOrientation (Gtk.Orientation.Horizontal);

		topBox.Append (colorDisplayArea);
		topBox.Append (colorCircleBox);
		topBox.Append (sliders);





		mainVbox.Append (topBox);

		// --- Initialization (Gtk.Window)

		Title = Translations.GetString ("Color Picker (UNFINISHED)");
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

	private void HandleSelectPrimSec (ListBox sender, ListBox.RowSelectedSignalArgs args)
	{
		if (args.Row.GetIndex () == 0) {
			editingPrimaryColor = true;
			currentColor = primaryColor;
		} else {
			editingPrimaryColor = false;
			currentColor = secondaryColor;
		}
		ColorCircleCursor.QueueDraw ();
	}

	private void DrawCursor (Context g)
	{
		var loc = HsvToLocation (currentColor.Hsv (), ColorCircleRadius);
		loc = new PointD (loc.X + ColorCircleRadius + CirclePadding, loc.Y + ColorCircleRadius + CirclePadding);

		g.DrawRectangle (new RectangleD (loc.X - 5, loc.Y - 5, 10, 10), new Color (0, 0, 0), 4);
		g.DrawRectangle (new RectangleD (loc.X - 5, loc.Y - 5, 10, 10), new Color (1, 1, 1), 1);
	}

	private void DrawValue (Context g)
	{
		var blackness = 1.0 - currentColor.Val ();

		if (!showValue)
			blackness = 0;

		g.Antialias = Antialias.None;
		g.FillEllipse (new RectangleD (CirclePadding, CirclePadding, ColorCircleRadius * 2 + 1, ColorCircleRadius * 2 + 1), new Color (0, 0, 0, blackness));


	}

	private void DrawColorDisplay (Context g, Color c)
	{
		int xy = ColorDisplayBorderSize;
		int wh = ColorDisplaySize - ColorDisplayBorderSize * 2;
		g.Antialias = Antialias.None;

		// make checker pattern
		if (c.A != 1) {
			g.FillRectangle (new RectangleD (xy, xy, wh, wh), new Color(1,1,1));
			g.FillRectangle (new RectangleD (xy, xy, wh / 2, wh / 2), new Color(.8,.8,.8));
			g.FillRectangle (new RectangleD (xy + wh / 2, xy + wh / 2, wh / 2, wh / 2), new Color(.8,.8,.8));
		}

		g.FillRectangle (new RectangleD (xy, xy, wh, wh), c);
		g.DrawRectangle (new RectangleD (xy, xy, wh, wh), new Color(0,0,0), ColorDisplayBorderSize);
	}

	// INCREDIBLY inefficient!!!
	private void DrawColorCircle (Context g)
	{
		//g.DrawEllipse (new RectangleD (3, 3, 194, 194), PintaCore.Palette.SecondaryColor, 6);


		PointI center = new PointI (ColorCircleRadius, ColorCircleRadius);

		for (int y = 0; y <= ColorCircleRadius * 2; y++) {
			for (int x = 0; x <= ColorCircleRadius * 2; x++) {
				PointI pxl = new PointI (x, y);
				PointI vec = pxl - center;
				if (vec.Magnitude () <= ColorCircleRadius - 1) {
					var hue = (MathF.Atan2 (-vec.X, vec.Y) + MathF.PI) / (2f * MathF.PI) * 360f;

					var sat = Math.Min (vec.Magnitude () / ColorCircleRadius, 1);

					var hsv = new HsvColor ((int) hue, (int) (sat * 100), 100);
					var rgb = hsv.ToRgb ();
					g.DrawRectangle (new RectangleD (x + CirclePadding, y + CirclePadding, 1, 1), new Color (rgb.Red / 255.0, rgb.Green / 255.0, rgb.Blue / 255.0), 2);
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
		double x;
		double y;
		ColorCircle.TranslateCoordinates (this, CirclePadding, CirclePadding, out x, out y);

		PointI centre = new PointI (100, 100);
		PointI cursor = new PointI ((int)(point.X - x), (int)(point.Y - y));

		PointI vecCursor = cursor - centre;

		// Numbers from thin air!
		var hue = (MathF.Atan2 (-vecCursor.X, vecCursor.Y) + MathF.PI) / (2f * MathF.PI) * 360f;

		var sat = Math.Min(vecCursor.Magnitude () / 100.0, 1);

		currentColor.SetHsv (hue: hue, saturation: sat);
		SetColorFromHsv ();
	}

	bool SetColorFromHsv ()
	{
		HueWidget.SetValue (currentColor.Hue ());
		SatWidget.SetValue (currentColor.Sat () * 100.0);
		ValWidget.SetValue (currentColor.Val () * 100.0);

		RWidget.SetValue (currentColor.R * 255.0);
		GWidget.SetValue (currentColor.G * 255.0);
		BWidget.SetValue (currentColor.B * 255.0);
		AWidget.SetValue (currentColor.A * 255.0);

		if(GetFocus ()?.Parent != HexEntry)
			HexEntry.SetText (currentColor.ToHex ());

		if (editingPrimaryColor) {
			primaryColor = currentColor;
			ColorDisplayPrimary.QueueDraw ();
		} else {
			secondaryColor = currentColor;
			ColorDisplaySecondary.QueueDraw ();
		}

		ColorCircleCursor.QueueDraw ();
		return true;
	}
}
