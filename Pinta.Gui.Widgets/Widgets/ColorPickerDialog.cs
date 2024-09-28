using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Adw;
using Cairo;
using Cairo.Internal;
using Gdk;
using GdkPixbuf;
using GLib;
using GObject;
using Gtk;
using Pango;
using Pinta.Core;
using Pinta.Gui.Widgets;
using Color = Cairo.Color;
using Context = Cairo.Context;
using HeaderBar = Adw.HeaderBar;
using MessageDialog = Adw.MessageDialog;
using Pattern = Cairo.Pattern;
using String = System.String;

namespace Pinta.Core;

// TODO LIST
/* translation strings
 * Modify swatches
 * Entries expanding on window expand
 */

public static class ColorExtensions
{

	// todo: maybe handle no-alpha output?

	public static String ToHex (this Color c)
	{
		int r = Convert.ToInt32(c.R * 255.0);
		int g = Convert.ToInt32(c.G * 255.0);
		int b = Convert.ToInt32(c.B * 255.0);
		int a = Convert.ToInt32(c.A * 255.0);

		return $"{r:X2}{g:X2}{b:X2}{a:X2}";
	}

	// todo: handle # in front of hex
	public static Color? FromHex (String hex)
	{
		if (hex.Length != 6 && hex.Length != 8)
			return null;
		try {

			int r = int.Parse (hex.Substring (0, 2), NumberStyles.HexNumber);
			int g = int.Parse (hex.Substring (2, 2), NumberStyles.HexNumber);
			int b = int.Parse (hex.Substring (4, 2), NumberStyles.HexNumber);
			int a = 255;
			if (hex.Length > 6)
				a = int.Parse (hex.Substring (5, 2), NumberStyles.HexNumber);

			Console.WriteLine($"{r}, {g}, {b}, {a}");

			return new Color().SetRgba (r / 255.0, g / 255.0, b / 255.0, a / 255.0);
		} catch {
			return null;
		}
	}

	// Copied from RgbColor.ToHsv
	// h, s, v: 0 <= h <= 360; 0 <= s,v <= 1
	public static Tuple<double, double, double> GetHsv (this Color c)
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
		return c.GetHsv ().Item1;
	}

	public static Color Copy (this Color c)
	{
		return new Color (c.R, c.G, c.B, c.A);
	}

	public static double Sat (this Color c)
	{
		return c.GetHsv ().Item2;
	}

	public static double Val (this Color c)
	{
		return c.GetHsv ().Item3;
	}

	public static Color SetRgba (this Color c, double? r = null, double? g = null, double? b = null, double? a = null)
	{
		return new Color (r ?? c.R, g ?? c.G, b ?? c.B, a ?? c.A);
	}

	public static Color SetHsv (this Color c, double? hue = null, double? sat = null, double? value = null)
	{
		var hsv = c.GetHsv ();

		double h = hue ?? hsv.Item1;
		double s = sat ?? hsv.Item2;
		double v = value ?? hsv.Item3;

		return FromHsv (h, s, v, c.A);
	}

	// Copied from HsvColor.ToRgb
	public static Color FromHsv (double hue, double sat, double value, double alpha)
	{
		double h = hue;
		double s = sat;
		double v = value;

		// Stupid hack!
		// If v or s is set to 0, it results in data loss for hue / sat. So we force it to be slightly above zero.
		if (v == 0)
			v = 0.0001;
		if (s == 0)
			s = 0.0001;

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
		return new Color (r, g, b, alpha);
	}
}
// used for the right hand side sliders
	// uses a label, scale, and entry
	// then hides the scale and draws over it
	// with a drawingarea
public class ColorPickerSlider : Gtk.Box
	{
		private readonly Gtk.Window topWindow;

		public Gtk.Label label = new Gtk.Label ();
		public Gtk.Scale slider = new Gtk.Scale ();
		public Gtk.Entry input = new Gtk.Entry ();
		public Gtk.DrawingArea gradient = new Gtk.DrawingArea ();
		public Gtk.DrawingArea cursor = new Gtk.DrawingArea ();

		public int maxVal;

		public class OnChangeValArgs : EventArgs
		{
			public string sender_name = "";
			public double value;
		}


		private bool entryBeingEdited = false;
		public ColorPickerSlider (int upper, String text, double val, Gtk.Window topWindow, int sliderPadding)
		{
			//this.Spacing = spacing;
			maxVal = upper;
			this.topWindow = topWindow;
			label.SetLabel (text);
			label.WidthRequest = 50;
			slider.SetOrientation (Orientation.Horizontal);
			slider.SetAdjustment (Adjustment.New (0, 0, maxVal + 1, 1, 1, 1));

			slider.WidthRequest = 200;
			slider.SetValue (val);
			slider.Opacity = 0;

			gradient.SetSizeRequest (200, this.GetHeight ());
			cursor.SetSizeRequest (200, this.GetHeight ());

			cursor.SetDrawFunc ((area, context, width, height) => {
				int outlineWidth = 2;

				var prog = slider.GetValue () / maxVal * (width - 2 * sliderPadding);

				ReadOnlySpan<PointD> points = stackalloc PointD[] {
					new PointD (prog + sliderPadding, height / 2),
					new PointD (prog + sliderPadding + 4, 3 * height / 4),
					new PointD (prog + sliderPadding + 4, height - outlineWidth / 2),
					new PointD (prog + sliderPadding - 4, height - outlineWidth / 2),
					new PointD (prog + sliderPadding - 4, 3 * height / 4),
					new PointD (prog + sliderPadding, height / 2)
				};

				context.LineWidth = outlineWidth;
				context.DrawPolygonal (points, new Color (0, 0, 0), LineCap.Butt);
				context.FillPolygonal (points, new Color (1,1,1));
			});



			var sliderOverlay = new Gtk.Overlay ();
			sliderOverlay.WidthRequest = 200;
			sliderOverlay.HeightRequest = this.GetHeight ();


			sliderOverlay.AddOverlay (gradient);
			sliderOverlay.AddOverlay (cursor);
			sliderOverlay.AddOverlay (slider);

			var sliderDraw = new Gtk.DrawingArea ();
			sliderDraw.WidthRequest = 200;
			sliderDraw.HeightRequest = 50;

			input.WidthRequest = 50;
			input.Hexpand = false;
			input.SetText (Convert.ToInt32(val).ToString());
			this.Append (label);
			this.Append (sliderOverlay);
			this.Append (input);

			//slider.Opacity = 0;

			slider.OnChangeValue += (sender, args) => {
				var e = new OnChangeValArgs ();


				e.sender_name = label.GetLabel ();
				e.value = args.Value;
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
					e2.sender_name = label.GetLabel ();
					e2.value = val;
					OnValueChange?.Invoke (this, e2);
				}
			});

		}

		public event EventHandler<OnChangeValArgs> OnValueChange;

		private int suppressEvent = 0;
		public void SetValue (double val)
		{
			slider.SetValue (val);
			// Make sure we do not set the text if we are editing it right now
			if (topWindow.GetFocus ()?.Parent != input) {
				// hackjob
				// prevents OnValueChange from firing when we change the value internally
				// because OnValueChange eventually calls SetValue so it causes a stack overflow
				suppressEvent = 2;
				input.SetText (Convert.ToInt32(val).ToString());
			}
			gradient.QueueDraw ();
			cursor.QueueDraw ();
		}

	}

public class CheckboxOption : Gtk.Box
{
	public bool state = false;
	public readonly Gtk.CheckButton button;
	public readonly Gtk.Label label;
	public CheckboxOption (int spacing, bool active, string text)
	{
		button = new Gtk.CheckButton ();
		state = active;
		button.Active = state;
		this.Append (button);

		label = new Gtk.Label { Label_ = "Show Value" };
		this.Append (label);
	}

	public void Toggle ()
	{
		state = !state;
		button.Active = state;
	}
}


public sealed class ColorPickerDialog : Gtk.Dialog
{
	private readonly int palette_display_size = 50;
	private readonly int palette_display_border_thickness = 3;
	private readonly Gtk.DrawingArea palette_display_primary;
	private readonly Gtk.DrawingArea palette_display_secondary;


	private readonly int picker_surface_radius = 200 / 2;
	private readonly int picker_surface_padding = 10;
	private readonly Gtk.DrawingArea picker_surface;
	private readonly Gtk.DrawingArea picker_surface_cursor;
	enum ColorSurfaceType
	{
		HueAndSat,
		SatAndVal
	}
	private ColorSurfaceType picker_surface_type = ColorSurfaceType.HueAndSat;

	private bool mouse_on_picker_surface = false;
	private readonly CheckboxOption picker_surface_option_draw_value;


	private readonly Entry hex_entry;

	private readonly int cps_padding_height = 10;
	private readonly int cps_padding_width = 14;
	private readonly ColorPickerSlider hue_cps;
	private readonly ColorPickerSlider sat_cps;
	private readonly ColorPickerSlider val_cps;

	private readonly ColorPickerSlider r_cps;
	private readonly ColorPickerSlider g_cps;
	private readonly ColorPickerSlider b_cps;

	private readonly ColorPickerSlider a_cps;


	//private Color current_color;
	private bool is_editing_primary_color = true;
	public Color primary_color;
	public Color secondary_color;

	public Color CurrentColor {
		get {
			if (is_editing_primary_color)
				return primary_color;
			else
				return secondary_color;
		}
		set {
			if (is_editing_primary_color)
				primary_color = value;
			else
				secondary_color = value;
		}
	}



	public ColorPickerDialog (ChromeManager chrome, PaletteManager palette, bool isPrimaryColor)
	{
		var tbar = new HeaderBar ();
		var reset_button = new Button ();
		reset_button.Label = Translations.GetString("Reset Color");
		reset_button.OnClicked += (button, args) => {
			primary_color = palette.PrimaryColor;
			secondary_color = palette.SecondaryColor;
			UpdateView ();
		};
		tbar.PackStart (reset_button);
		this.SetTitlebar (tbar);

		is_editing_primary_color = isPrimaryColor;
		const int spacing = 6;

		primary_color = palette.PrimaryColor;
		secondary_color = palette.SecondaryColor;


		var topBox = new Gtk.Box {Spacing = spacing};

		#region Active Palette Display
		var colorDisplayArea = new Gtk.ListBox ();

		palette_display_primary = new Gtk.DrawingArea ();
		palette_display_primary.SetSizeRequest (palette_display_size, palette_display_size);
		palette_display_primary.SetDrawFunc ((area, context, width, height) => DrawPaletteDisplay (context, primary_color));

		colorDisplayArea.Append (palette_display_primary);

		palette_display_secondary = new Gtk.DrawingArea ();
		palette_display_secondary.SetSizeRequest (palette_display_size, palette_display_size);
		palette_display_secondary.SetDrawFunc ((area, context, width, height) => DrawPaletteDisplay (context, secondary_color));

		colorDisplayArea.Append (palette_display_secondary);

		if (is_editing_primary_color)
			colorDisplayArea.SelectRow (colorDisplayArea.GetRowAtIndex (0));
		else
			colorDisplayArea.SelectRow (colorDisplayArea.GetRowAtIndex (1));
		colorDisplayArea.SetSelectionMode (SelectionMode.Single);

		colorDisplayArea.OnRowSelected += HandlePaletteSelect;

		#endregion

		#region Picker Surface

		var pickerSurfaceDrawSize = (picker_surface_radius + picker_surface_padding) * 2;

		var pickerSurfaceBox = new Gtk.Box { Spacing = spacing, WidthRequest = pickerSurfaceDrawSize };
		pickerSurfaceBox.SetOrientation (Orientation.Vertical);

		var pickerSurfaceType = new Gtk.Box { Spacing = spacing, WidthRequest = pickerSurfaceDrawSize };
		pickerSurfaceType.Homogeneous = true;
		pickerSurfaceType.Halign = Align.Center;


		// TODO: Remember setting!
		// When Gir.Core supports it, this should probably be replaced with a toggle group.
		var pickerSurfaceHueSat = Gtk.ToggleButton.NewWithLabel ("Hue & Sat");
		if(picker_surface_type == ColorSurfaceType.HueAndSat)
			pickerSurfaceHueSat.Toggle ();
		pickerSurfaceHueSat.OnToggled += (sender, args) => {
			picker_surface_type = ColorSurfaceType.HueAndSat;
			picker_surface_option_draw_value?.SetOpacity (1);
			UpdateView ();
		};

		var pickerSurfaceSatVal = Gtk.ToggleButton.NewWithLabel ("Sat & Value");
		if(picker_surface_type == ColorSurfaceType.SatAndVal)
			pickerSurfaceSatVal.Toggle ();
		pickerSurfaceSatVal.OnToggled += (sender, args) => {
			picker_surface_type = ColorSurfaceType.SatAndVal;
			picker_surface_option_draw_value?.SetOpacity (0);
			UpdateView ();
		};


		pickerSurfaceHueSat.SetGroup (pickerSurfaceSatVal);
		pickerSurfaceType.Append (pickerSurfaceHueSat);
		pickerSurfaceType.Append (pickerSurfaceSatVal);

		pickerSurfaceBox.Append (pickerSurfaceType);

		picker_surface = new Gtk.DrawingArea ();
		picker_surface.WidthRequest = pickerSurfaceDrawSize;
		picker_surface.HeightRequest = pickerSurfaceDrawSize;
		picker_surface.SetDrawFunc ((area, context, width, height) => DrawColorSurface (context));

		// Cursor handles the square in the picker surface displaying where your selected color is
		picker_surface_cursor = new Gtk.DrawingArea ();
		picker_surface_cursor.WidthRequest = pickerSurfaceDrawSize;
		picker_surface_cursor.HeightRequest = pickerSurfaceDrawSize;
		picker_surface_cursor.SetDrawFunc ((area, context, width, height) => DrawCursor (context));

		// Overlays the cursor on top of the surface
		var pickerSurfaceOverlay = new Gtk.Overlay ();
		pickerSurfaceOverlay.AddOverlay (picker_surface);
		pickerSurfaceOverlay.AddOverlay (picker_surface_cursor);
		pickerSurfaceOverlay.HeightRequest = pickerSurfaceDrawSize;
		pickerSurfaceOverlay.WidthRequest = pickerSurfaceDrawSize;

		pickerSurfaceBox.Append (pickerSurfaceOverlay);

		// Show Value toggle for hue sat picker surface
		// TODO: Remember setting!
		picker_surface_option_draw_value = new CheckboxOption (spacing, true, "Show Value");
		picker_surface_option_draw_value.button.OnToggled += (o, e) => {
			picker_surface_option_draw_value.Toggle ();
			UpdateView ();
		};
		pickerSurfaceBox.Append (picker_surface_option_draw_value);

		#endregion

		#region SliderAndHex

		var sliders = new Gtk.Box {Spacing = spacing};
		sliders.SetOrientation (Orientation.Vertical);

		var hexBox = new Gtk.Box { Spacing = spacing };

		hexBox.Append (new Label { Label_ = "Hex", WidthRequest = 50});
		hex_entry = new Entry { Text_ = CurrentColor.ToHex ()};
		hex_entry.OnChanged ((o, e) => {
			if (GetFocus ()?.Parent == hex_entry) {
				CurrentColor = ColorExtensions.FromHex (hex_entry.GetText ()) ?? CurrentColor;
				UpdateView ();
			}
		});

		hexBox.Append (hex_entry);


		sliders.Append (hexBox);


		hue_cps = new ColorPickerSlider (360, "Hue", CurrentColor.Hue (), this, cps_padding_width);
		hue_cps.OnValueChange += (sender, args) => {
			CurrentColor = CurrentColor.SetHsv (hue: args.value);
			UpdateView ();
		};
		hue_cps.gradient.SetDrawFunc ((area, context, width, height) =>
			DrawGradient (context, width, height, [
				CurrentColor.SetHsv (hue: 0),
				CurrentColor.SetHsv (hue: 60),
				CurrentColor.SetHsv (hue: 120),
				CurrentColor.SetHsv (hue: 180),
				CurrentColor.SetHsv (hue: 240),
				CurrentColor.SetHsv (hue: 300),
				CurrentColor.SetHsv (hue: 360)
			]));
		sliders.Append (hue_cps);

		sat_cps = new ColorPickerSlider (100, "Sat", CurrentColor.Sat () * 100.0, this, cps_padding_width);
		sat_cps.OnValueChange += (sender, args) => {
			CurrentColor = CurrentColor.SetHsv (sat: args.value / 100.0);
			UpdateView ();
		};
		sat_cps.gradient.SetDrawFunc ((area, context, width, height) =>
			DrawGradient (context, width, height, [
				CurrentColor.SetHsv (sat: 0),
				CurrentColor.SetHsv (sat: 1)
			]));
		sliders.Append (sat_cps);


		val_cps = new ColorPickerSlider (100, "Value", CurrentColor.Val () * 100.0, this, cps_padding_width);
		val_cps.OnValueChange += (sender, args) => {
			CurrentColor = CurrentColor.SetHsv (value: args.value / 100.0);
			UpdateView ();
		};
		val_cps.gradient.SetDrawFunc ((area, context, width, height) =>
			DrawGradient (context, width, height, [
				CurrentColor.SetHsv (value: 0),
				CurrentColor.SetHsv (value: 1)
			]));
		sliders.Append (val_cps);

		sliders.Append (new Gtk.Separator());

		r_cps = new ColorPickerSlider (255, "Red", CurrentColor.R * 255.0, this, cps_padding_width);
		r_cps.OnValueChange += (sender, args) => {
			CurrentColor = CurrentColor.SetRgba (r: args.value / 255.0);
			UpdateView ();
		};
		r_cps.gradient.SetDrawFunc ((area, context, width, height) =>
			DrawGradient (context, width, height, [CurrentColor.SetRgba (r: 0), CurrentColor.SetRgba (r: 1)]));

		sliders.Append (r_cps);
		g_cps = new ColorPickerSlider (255, "Green", CurrentColor.G * 255.0, this, cps_padding_width);
		g_cps.OnValueChange += (sender, args) => {
			CurrentColor = CurrentColor.SetRgba (g: args.value / 255.0);
			UpdateView ();
		};
		g_cps.gradient.SetDrawFunc ((area, context, width, height) =>
			DrawGradient (context, width, height, [CurrentColor.SetRgba (g: 0), CurrentColor.SetRgba (g: 1)]));
		sliders.Append (g_cps);
		b_cps = new ColorPickerSlider (255, "Blue", CurrentColor.B * 255.0, this, cps_padding_width);
		b_cps.OnValueChange += (sender, args) => {
			CurrentColor = CurrentColor.SetRgba (b: args.value / 255.0);
			UpdateView ();
		};
		b_cps.gradient.SetDrawFunc ((area, context, width, height) =>
			DrawGradient (context, width, height, [CurrentColor.SetRgba (b: 0), CurrentColor.SetRgba (b: 1)]));
		sliders.Append (b_cps);
		sliders.Append (new Gtk.Separator());
		a_cps = new ColorPickerSlider (255, "Alpha", CurrentColor.A * 255.0, this, cps_padding_width);
		a_cps.OnValueChange += (sender, args) => {
			CurrentColor = CurrentColor.SetRgba (a: args.value / 255.0);
			UpdateView ();
		};
		a_cps.gradient.SetDrawFunc ((area, context, width, height) =>
			DrawGradient (context, width, height, [CurrentColor.SetRgba (a: 0), CurrentColor.SetRgba (a: 1)]));
		sliders.Append (a_cps);

		#endregion

		#region Swatch

		// 90% taken from SatsuBarColorPaletteWidget
		var swatchBox = new Gtk.Box { Spacing = spacing };
		swatchBox.SetOrientation (Orientation.Vertical);
		var recentPaletteSwatches = new DrawingArea ();
		recentPaletteSwatches.WidthRequest =  500;
		recentPaletteSwatches.HeightRequest = StatusBarColorPaletteWidget.SWATCH_SIZE * StatusBarColorPaletteWidget.PALETTE_ROWS;

		recentPaletteSwatches.SetDrawFunc ((area, g, width, height) => {
			var recent = PintaCore.Palette.RecentlyUsedColors;
			var recent_cols = PintaCore.Palette.MaxRecentlyUsedColor / StatusBarColorPaletteWidget.PALETTE_ROWS;
			var recent_palette_rect = new RectangleD (0, 0, StatusBarColorPaletteWidget.SWATCH_SIZE * recent_cols,
				StatusBarColorPaletteWidget.SWATCH_SIZE * StatusBarColorPaletteWidget.PALETTE_ROWS);

			for (var i = 0; i < recent.Count (); i++)
				g.FillRectangle (StatusBarColorPaletteWidget.GetSwatchBounds (i, recent_palette_rect, true), recent.ElementAt (i));
		});

		swatchBox.Append (recentPaletteSwatches);


		var paletteSwatches = new DrawingArea ();

		paletteSwatches.WidthRequest =  500;
		paletteSwatches.HeightRequest = StatusBarColorPaletteWidget.SWATCH_SIZE * StatusBarColorPaletteWidget.PALETTE_ROWS;

		paletteSwatches.SetDrawFunc ((area, g, width, height) => {
			var palette_rect = new RectangleD (0, 0,
				width - StatusBarColorPaletteWidget.PALETTE_MARGIN,
				StatusBarColorPaletteWidget.SWATCH_SIZE * StatusBarColorPaletteWidget.PALETTE_ROWS);

			var palette = PintaCore.Palette.CurrentPalette;
			for (var i = 0; i < palette.Count; i++)
				g.FillRectangle (StatusBarColorPaletteWidget.GetSwatchBounds (i, palette_rect), palette[i]);
		});
		swatchBox.Append (paletteSwatches);

		#endregion

		#region Mouse Handler

		var click_gesture = Gtk.GestureClick.New ();
		click_gesture.SetButton (0); // Listen for all mouse buttons.
		click_gesture.OnPressed += (_, e) => {

			PointD absPos = new PointD (e.X, e.Y);
			PointD relPos;

			if (IsMouseInDrawingArea (this, picker_surface, absPos, out relPos)) {
				mouse_on_picker_surface = true;
				SetColorFromPickerSurface(new PointD (e.X, e.Y));
			} else

			if (IsMouseInDrawingArea (this, recentPaletteSwatches, absPos, out relPos)) {
				var recent_index = StatusBarColorPaletteWidget.GetSwatchAtLocation (relPos, new RectangleD(), true);

				if (recent_index >= 0) {
					CurrentColor = PintaCore.Palette.CurrentPalette[recent_index];
					UpdateView ();
				}
			} else

			if (IsMouseInDrawingArea (this, paletteSwatches, absPos, out relPos)) {
				var index = StatusBarColorPaletteWidget.GetSwatchAtLocation (relPos, new RectangleD());

				if (index >= 0) {
					CurrentColor = PintaCore.Palette.CurrentPalette[index];
					UpdateView ();
				}
			}

		};
		click_gesture.OnReleased += (_, e) => {
			mouse_on_picker_surface = false;
		};
		AddController (click_gesture);

		var motion_controller = Gtk.EventControllerMotion.New ();
		motion_controller.OnMotion += (_, args) => {

			if (mouse_on_picker_surface)
				SetColorFromPickerSurface(new PointD (args.X, args.Y));
		};
		AddController (motion_controller);

		#endregion


		Gtk.Box mainVbox = new () { Spacing = spacing };
		mainVbox.SetOrientation (Gtk.Orientation.Vertical);

		topBox.Append (colorDisplayArea);
		topBox.Append (pickerSurfaceBox);
		topBox.Append (sliders);


		mainVbox.Append (topBox);
		mainVbox.Append (swatchBox);

		// --- Initialization (Gtk.Window)

		Title = Translations.GetString ("Color Picker");
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

	private void UpdateView ()
	{
		// Redraw picker surfaces
		picker_surface_cursor.QueueDraw ();
		picker_surface.QueueDraw ();

		// Redraw cps
		hue_cps.SetValue (CurrentColor.Hue ());
		sat_cps.SetValue (CurrentColor.Sat () * 100.0);
		val_cps.SetValue (CurrentColor.Val () * 100.0);

		r_cps.SetValue (CurrentColor.R * 255.0);
		g_cps.SetValue (CurrentColor.G * 255.0);
		b_cps.SetValue (CurrentColor.B * 255.0);
		a_cps.SetValue (CurrentColor.A * 255.0);


		// Update hex
		if(GetFocus ()?.Parent != hex_entry)
			hex_entry.SetText (CurrentColor.ToHex ());

		// Redraw palette displays
		palette_display_primary.QueueDraw ();
		palette_display_secondary.QueueDraw ();
	}

	public static bool IsMouseInDrawingArea (Widget topWidget, Widget area, PointD mousePos, out PointD relPos, PointD? offset = null)
	{
		var off = offset ?? new PointD (0, 0);
		area.TranslateCoordinates (topWidget, off.X, off.Y, out double x, out double y);
		relPos = new PointD ((mousePos.X - x), (mousePos.Y - y));
		if (relPos.X >= 0 && relPos.X <= area.WidthRequest && relPos.Y >= 0 && relPos.Y <= area.HeightRequest) {
			return true;
		} else {
			return false;
		}
	}

	private void HandlePaletteSelect (ListBox sender, ListBox.RowSelectedSignalArgs args)
	{
		if (args.Row?.GetIndex () == 0)
			is_editing_primary_color = true;
		else
			is_editing_primary_color = false;

		picker_surface_cursor.QueueDraw ();
		UpdateView ();
	}

	private void DrawCursor (Context g)
	{
		g.Antialias = Antialias.None;
		var loc = HsvToPickerLocation (CurrentColor.GetHsv (), picker_surface_radius);
		loc = new PointD (loc.X + picker_surface_radius + picker_surface_padding, loc.Y + picker_surface_radius + picker_surface_padding);

		g.FillRectangle (new RectangleD (loc.X - 5, loc.Y - 5, 10, 10), CurrentColor);
		g.DrawRectangle (new RectangleD (loc.X - 5, loc.Y - 5, 10, 10), new Color (0, 0, 0), 4);
		g.DrawRectangle (new RectangleD (loc.X - 5, loc.Y - 5, 10, 10), new Color (1, 1, 1), 1);
	}

	private void DrawGradient (Context context, int width, int height, Color[] colors)
	{
		context.Antialias = Antialias.None;
		var x0 = cps_padding_width;
		var y0 = cps_padding_height;
		var draw_w = width - cps_padding_width * 2;
		var draw_h = height - cps_padding_height * 2;
		var x1 = x0 + draw_w;
		var y1 = y0 + draw_h;

		var bsize = draw_h / 2;
		var blocks = (int)Math.Floor((double)bsize / width);

		// Draw transparency background
		// todo merge fors
		context.FillRectangle (new RectangleD (x0, y0, draw_w, draw_h), new Color(1,1,1));
		for (int x = x0; x < x1; x += bsize * 2) {
			var bwidth = bsize;
			if (x + bsize > x1)
				bwidth = x1 - x;
			context.FillRectangle (new RectangleD (x, y0, bwidth, bsize), new Color(.8,.8,.8));
		}

		for (int x = x0 + bsize; x < x1; x += bsize * 2) {
			var bwidth = bsize;
			if (x + bsize > x1)
				bwidth = x1 - x;
			context.FillRectangle (new RectangleD (x, y0 + draw_h / 2, bwidth, bsize), new Color (.8, .8, .8));
		}

		var pat = new LinearGradient (x0, y0, x1, y1);

		for (int i = 0; i < colors.Length; i++)
			pat.AddColorStop (i / (double)(colors.Length - 1), colors[i]);

		context.Rectangle (x0, y0, draw_w, draw_h);
		context.SetSource (pat);
		context.Fill ();
	}

	private void DrawPaletteDisplay (Context g, Color c)
	{
		int xy = palette_display_border_thickness;
		int wh = palette_display_size - palette_display_border_thickness * 2;
		g.Antialias = Antialias.None;

		// make checker pattern
		if (c.A != 1) {
			g.FillRectangle (new RectangleD (xy, xy, wh, wh), new Color(1,1,1));
			g.FillRectangle (new RectangleD (xy, xy, wh / 2, wh / 2), new Color(.8,.8,.8));
			g.FillRectangle (new RectangleD (xy + wh / 2, xy + wh / 2, wh / 2, wh / 2), new Color(.8,.8,.8));
		}

		g.FillRectangle (new RectangleD (xy, xy, wh, wh), c);
		g.DrawRectangle (new RectangleD (xy, xy, wh, wh), new Color(0,0,0), palette_display_border_thickness);
	}

	private void DrawColorSurface (Context g)
	{
		int rad = picker_surface_radius;
		int x0 = picker_surface_padding;
		int y0 = picker_surface_padding;
		int draw_width = picker_surface_radius * 2;
		int draw_height = picker_surface_radius * 2;
		int x1 = x0 + picker_surface_padding;
		int y1 = y0 + picker_surface_padding;
		PointI center = new PointI (rad, rad);

		// todo: change this using memtexture method

		if (picker_surface_type == ColorSurfaceType.HueAndSat) {
			int stride = draw_width * 4;

			Span<byte> data = stackalloc byte[draw_height * stride];

			for (int y = 0; y < draw_height; y++) {
				for (int x = 0; x < draw_width; x++) {
					PointI pxl = new PointI (x, y);
					PointI vec = pxl - center;
					if (vec.Magnitude () <= rad - 1) {
						var h = (MathF.Atan2 (vec.Y, -vec.X) + MathF.PI) / (2f * MathF.PI) * 360f;

						var s = Math.Min (vec.Magnitude () / rad, 1);

						double v = 1;
						if (picker_surface_option_draw_value.state)
							v = CurrentColor.Val ();

						var c = ColorExtensions.FromHsv (h, s, v, 1);

						data[(y * stride) + (x * 4) + 0] = (byte)(c.R * 255);
						data[(y * stride) + (x * 4) + 1] = (byte)(c.G * 255);
						data[(y * stride) + (x * 4) + 2] = (byte)(c.B * 255);
						data[(y * stride) + (x * 4) + 3] = (byte)(255);
					} else {
						data[(y * stride) + (x * 4) + 0] = (byte)(0);
						data[(y * stride) + (x * 4) + 1] = (byte)(0);
						data[(y * stride) + (x * 4) + 2] = (byte)(0);
						data[(y * stride) + (x * 4) + 3] = (byte)(0);
					}
				}
			}

			var img = MemoryTexture.New (draw_width, draw_height, MemoryFormat.R8g8b8a8, Bytes.New (data), (UIntPtr)stride).ToSurface ();
			g.SetSourceSurface (img, picker_surface_padding, picker_surface_padding);
			g.Paint ();
		} else if (picker_surface_type == ColorSurfaceType.SatAndVal) {
			int stride = draw_width * 3;

			Span<byte> data = stackalloc byte[draw_height * stride];

			for (int y = 0; y < draw_height; y++) {
				double s = 1.0 - (double)y / (draw_height - 1);
				for (int x = 0; x < draw_width; x++) {
					double v = (double) x / (draw_width - 1);
					var c = ColorExtensions.FromHsv (CurrentColor.Hue (), s, v, 1);
					data[(y * stride) + (x * 3) + 0] = (byte)(c.R * 255);
					data[(y * stride) + (x * 3) + 1] = (byte)(c.G * 255);
					data[(y * stride) + (x * 3) + 2] = (byte)(c.B * 255);
				}
			}

			var img = MemoryTexture.New (draw_width, draw_height, MemoryFormat.R8g8b8, Bytes.New (data), (UIntPtr)stride).ToSurface ();
			g.SetSourceSurface (img, picker_surface_padding, picker_surface_padding);
			g.Paint ();
		}
	}

	// Takes in HSV values as tuple (h,s,v) and returns the position of that color in the picker surface.
	private PointD HsvToPickerLocation (Tuple<double, double, double> hsv, int radius)
	{
		if (picker_surface_type == ColorSurfaceType.HueAndSat) {
			var rad = hsv.Item1 * (Math.PI / 180.0);
			var mult = radius;
			var mag = hsv.Item2 * mult;
			var x = Math.Cos (rad) * mag;
			var y = Math.Sin (rad) * mag;
			return new PointD (x, -y);
		} else if (picker_surface_type == ColorSurfaceType.SatAndVal) {
			int size = radius * 2;
			var x = hsv.Item3 * (size - 1);
			var y = size - hsv.Item2 * (size - 1);
			return new PointD (x - radius, y - radius);
		}

		return new PointD (0, 0);
	}

	void SetColorFromPickerSurface (PointD point)
	{
		picker_surface.TranslateCoordinates (this, picker_surface_padding, picker_surface_padding, out var x, out var y);
		PointI centre = new PointI (picker_surface_radius, picker_surface_radius);
		PointI cursor = new PointI ((int) (point.X - x), (int) (point.Y - y));

		PointI vecCursor = cursor - centre;

		if (picker_surface_type == ColorSurfaceType.HueAndSat) {
			var hue = (MathF.Atan2 (vecCursor.Y, -vecCursor.X) + MathF.PI) / (2f * MathF.PI) * 360f;

			var sat = Math.Min (vecCursor.Magnitude () / 100.0, 1);

			CurrentColor = CurrentColor.SetHsv (hue: hue, sat: sat);
		} else if (picker_surface_type == ColorSurfaceType.SatAndVal) {
			int size = picker_surface_radius * 2;
			//todo: double
			if (cursor.X > size - 1)
				cursor = cursor with { X = size - 1 };
			if (cursor.X < 0)
				cursor = cursor with { X = 0 };
			if (cursor.Y > size - 1)
				cursor = cursor with { Y = size - 1 };
			if (cursor.Y < 0)
				cursor = cursor with { Y = 0 };
			float s = 1f - (float)cursor.Y / (size - 1);
			float v = (float)cursor.X / (size - 1);
			CurrentColor = CurrentColor.SetHsv (sat: s, value: v);
		}
		UpdateView ();
	}
}
