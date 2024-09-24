using System;
using System.Globalization;
using System.Linq;
using Adw;
using Cairo;
using GdkPixbuf;
using GObject;
using Gtk;
using Pango;
using Pinta.Core;
using Pinta.Gui.Widgets;
using Color = Cairo.Color;
using Context = Cairo.Context;
using HeaderBar = Adw.HeaderBar;
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

	public static Color? FromHex (this ref Color c, String hex)
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

			return c.SetRgba (r / 255.0, g / 255.0, b / 255.0, a / 255.0);
		} catch {
			return null;
		}
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

	public static Color Copy (this Color c)
	{
		return new Color (c.R, c.G, c.B, c.A);
	}

	public static double Sat (this Color c)
	{
		return c.Hsv ().Item2;
	}

	public static double Val (this Color c)
	{
		return c.Hsv ().Item3;
	}

	public static Color SetRgba (this ref Color c, double? r = null, double? g = null, double? b = null, double? a = null)
	{
		return new Color (r ?? c.R, g ?? c.G, b ?? c.B, a ?? c.A);
	}

	public static void ChangeRgba (this ref Color c, double? r = null, double? g = null, double? b = null, double? a = null)
	{
		c = new Color (r ?? c.R, g ?? c.G, b ?? c.B, a ?? c.A);
	}

	// See HsvColor.ToRgb
	public static Color SetHsv (this ref Color c, double? hue = null, double? saturation = null, double? value = null)
	{
		var hsv = c.Hsv ();

		double h = hue ?? hsv.Item1;
		double s = saturation ?? hsv.Item2;
		double v = value ?? hsv.Item3;

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
		return new Color (r, g, b, c.A);
	}
}

public sealed class ColorPickerDialog : Gtk.Dialog
{

	private readonly PaletteManager palette;
	private readonly WorkspaceManager workspace;


	private readonly int color_display_size = 50;
	private readonly int color_display_border_thickness = 3;
	private readonly Gtk.DrawingArea color_display_primary;
	private readonly Gtk.DrawingArea color_display_secondary;

	private readonly int color_circle_radius = 200 / 2;
	private readonly int color_circle_padding = 10;
	private readonly Gtk.DrawingArea color_circle_hue;
	private readonly Gtk.DrawingArea color_circle_value;
	private readonly Gtk.DrawingArea color_circle_cursor;


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


	private bool mouse_on_color_circle = false;
	private bool color_circle_show_value = true;

	private Color current_color;
	private bool is_editing_primary_color = true;
	public Color primary_color;
	public Color secondary_color;

	private double last_rendered_value = -1;

	//scale-entry input
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

				var prog = slider.GetValue () / maxVal * (width - 2 * sliderPadding);

				ReadOnlySpan<PointD> points = stackalloc PointD[5] {
					new PointD (prog + sliderPadding, height / 2),
					new PointD (prog + sliderPadding + 5, 3 * height / 4),
					new PointD (prog + sliderPadding + 5, height),
					new PointD (prog + sliderPadding - 5, height),
					new PointD (prog + sliderPadding - 5, 3 * height / 4)
				};
				context.FillPolygonal (points, new Color (1,1,1));
			});



			var sliderOverlay = new Gtk.Overlay ();
			sliderOverlay.WidthRequest = 200;
			sliderOverlay.HeightRequest = this.GetHeight ();


			sliderOverlay.AddOverlay (gradient);
			sliderOverlay.AddOverlay (cursor);
			sliderOverlay.AddOverlay (slider);

			input.WidthRequest = 50;
			input.SetText (Convert.ToInt32(val).ToString());
			this.Append (label);
			this.Append (sliderOverlay);
			this.Append (input);


			slider.OnChangeValue += (sender, args) => {
				if (suppressEvent > 0) {
					suppressEvent--;
					return false;
				}
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
			suppressEvent = 1;
			slider.SetValue (val);
			// Make sure we do not set the text if we are editing it right now
			if (topWindow.GetFocus ()?.Parent != input) {
				suppressEvent++;
				input.SetText (Convert.ToInt32(val).ToString());
			}
			gradient.QueueDraw ();
			cursor.QueueDraw ();
		}

	}

	public ColorPickerDialog (ChromeManager chrome, PaletteManager palette, bool isPrimaryColor)
	{
		var tbar = new HeaderBar ();
		var reset_button = new Button ();
		reset_button.Label = "Reset Color";
		reset_button.OnClicked += (button, args) => {
			primary_color = palette.PrimaryColor;
			secondary_color = palette.SecondaryColor;
			if (is_editing_primary_color)
				current_color = primary_color;
			else
				current_color = secondary_color;
		};
		tbar.PackStart (reset_button);
		this.SetTitlebar (tbar);

		is_editing_primary_color = isPrimaryColor;

		this.palette = palette;
		const int spacing = 6;

		if (is_editing_primary_color)
			current_color = palette.PrimaryColor;
		else
			current_color = palette.SecondaryColor;
		primary_color = palette.PrimaryColor;
		secondary_color = palette.SecondaryColor;


		var topBox = new Gtk.Box {Spacing = spacing};

		#region Color Display

		var colorDisplayArea = new Gtk.ListBox ();

		color_display_primary = new Gtk.DrawingArea ();
		color_display_primary.SetSizeRequest (color_display_size, color_display_size);
		color_display_primary.SetDrawFunc ((area, context, width, height) => DrawColorDisplay (context, primary_color));

		colorDisplayArea.Append (color_display_primary);

		color_display_secondary = new Gtk.DrawingArea ();
		color_display_secondary.SetSizeRequest (color_display_size, color_display_size);
		color_display_secondary.SetDrawFunc ((area, context, width, height) => DrawColorDisplay (context, secondary_color));

		colorDisplayArea.Append (color_display_secondary);

		if (is_editing_primary_color)
			colorDisplayArea.SelectRow (colorDisplayArea.GetRowAtIndex (0));
		else
			colorDisplayArea.SelectRow (colorDisplayArea.GetRowAtIndex (1));
		colorDisplayArea.SetSelectionMode (SelectionMode.Single);

		colorDisplayArea.OnRowSelected += HandlePaletteSelect;

		#endregion

		#region Color Circle

		var colorCircleBox = new Gtk.Box { Spacing = spacing };
		colorCircleBox.SetOrientation (Orientation.Vertical);

		var colorCircleSize = (color_circle_radius + color_circle_padding) * 2;

		color_circle_hue = new Gtk.DrawingArea ();
		color_circle_hue.WidthRequest = colorCircleSize;
		color_circle_hue.HeightRequest = colorCircleSize;
		color_circle_hue.SetDrawFunc ((area, context, width, height) => DrawColorCircle (context));

		color_circle_value = new Gtk.DrawingArea ();
		color_circle_value.WidthRequest = colorCircleSize;
		color_circle_value.HeightRequest = colorCircleSize;
		color_circle_value.SetDrawFunc ((area, context, width, height) => DrawValue (context));

		color_circle_cursor = new Gtk.DrawingArea ();
		color_circle_cursor.WidthRequest = colorCircleSize;
		color_circle_cursor.HeightRequest = colorCircleSize;
		color_circle_cursor.SetDrawFunc ((area, context, width, height) => DrawCursor (context));

		var colorCircleOverlay = new Gtk.Overlay ();
		colorCircleOverlay.AddOverlay (color_circle_hue);
		colorCircleOverlay.AddOverlay (color_circle_value);
		colorCircleOverlay.AddOverlay (color_circle_cursor);
		colorCircleOverlay.HeightRequest = colorCircleSize;
		colorCircleOverlay.WidthRequest = colorCircleSize;

		colorCircleBox.Append (colorCircleOverlay);

		// Show Value toggle
		var colorCircleValueToggleBox = new Gtk.Box ();

		// TODO: Remember setting!
		var colorCircleValueToggle = new Gtk.CheckButton ();
		colorCircleValueToggle.Active = color_circle_show_value;
		colorCircleValueToggle.OnToggled += (o, e) => {
			color_circle_show_value = !color_circle_show_value;
			colorCircleValueToggle.Active = color_circle_show_value;
			RedrawValue (true);
		};

		colorCircleValueToggleBox.Append (colorCircleValueToggle);

		var colorCircleValueToggleLabel = new Gtk.Label { Label_ = "Show Value" };
		colorCircleValueToggleBox.Append (colorCircleValueToggleLabel);

		colorCircleBox.Append (colorCircleValueToggleBox);

		#endregion

		#region SliderAndHex

		var sliders = new Gtk.Box {Spacing = spacing};
		sliders.SetOrientation (Orientation.Vertical);

		var hexBox = new Gtk.Box { Spacing = spacing };

		hexBox.Append (new Label { Label_ = "Hex", WidthRequest = 50});
		hex_entry = new Entry { Text_ = current_color.ToHex ()};
		hex_entry.OnChanged ((o, e) => {
			if (GetFocus ()?.Parent == hex_entry) {
				current_color = current_color.FromHex (hex_entry.GetText ()) ?? current_color;
				UpdateColorView ();
			}
		});

		hexBox.Append (hex_entry);


		sliders.Append (hexBox);


		hue_sei = new ScaleEntryInput (360, "Hue", current_color.Hue (), this);
		hue_sei.OnValueChange += (sender, args) => {
			current_color.SetHsv (hue: args.value);
			UpdateColorView ();
		};
		hue_cps.gradient.SetDrawFunc ((area, context, width, height) =>
			DrawGradient (context, width, height, [
				current_color.SetHsv (hue: 0),
				current_color.SetHsv (hue: 60),
				current_color.SetHsv (hue: 120),
				current_color.SetHsv (hue: 180),
				current_color.SetHsv (hue: 240),
				current_color.SetHsv (hue: 300),
				current_color.SetHsv (hue: 360)
			]));
		sliders.Append (hue_cps);

		sat_sei = new ScaleEntryInput (100, "Sat", current_color.Sat () * 100.0, this);
		sat_sei.OnValueChange += (sender, args) => {
			current_color.SetHsv (saturation: args.value / 100.0);
			UpdateColorView ();
		};
		sat_cps.gradient.SetDrawFunc ((area, context, width, height) =>
			DrawGradient (context, width, height, [
				current_color.SetHsv (saturation: 0),
				current_color.SetHsv (saturation: 1)
			]));
		sliders.Append (sat_cps);


		val_sei = new ScaleEntryInput (100, "Value", current_color.Val () * 100.0, this);
		val_sei.OnValueChange += (sender, args) => {
			current_color.SetHsv (value: args.value / 100.0);
			UpdateColorView ();
		};
		val_cps.gradient.SetDrawFunc ((area, context, width, height) =>
			DrawGradient (context, width, height, [
				current_color.SetHsv (value: 0),
				current_color.SetHsv (value: 1)
			]));
		sliders.Append (val_cps);

		sliders.Append (new Gtk.Separator());

		r_sei = new ScaleEntryInput (255, "Red", current_color.R * 255.0, this);
		r_sei.OnValueChange += (sender, args) => {
			current_color.SetRgba (r: args.value / 255.0);
			UpdateColorView ();
		};
		sliders.Append (r_sei);
		g_sei = new ScaleEntryInput (255, "Green", current_color.G * 255.0, this);
		g_sei.OnValueChange += (sender, args) => {
			current_color.SetRgba (g: args.value / 255.0);
			UpdateColorView ();
		};
		sliders.Append (g_sei);
		b_sei = new ScaleEntryInput (255, "Blue", current_color.B * 255.0, this);
		b_sei.OnValueChange += (sender, args) => {
			current_color.SetRgba (b: args.value / 255.0);
			UpdateColorView ();
		};
		b_cps.gradient.SetDrawFunc ((area, context, width, height) =>
			DrawGradient (context, width, height, [current_color.SetRgba (b: 0), current_color.SetRgba (b: 1)]));
		sliders.Append (b_cps);
		sliders.Append (new Gtk.Separator());
		a_sei = new ScaleEntryInput (255, "Alpha", current_color.A * 255.0, this);
		a_sei.OnValueChange += (sender, args) => {
			current_color.SetRgba (a: args.value / 255.0);
			UpdateColorView ();
		};
		a_cps.gradient.SetDrawFunc ((area, context, width, height) =>
			DrawGradient (context, width, height, [current_color.SetRgba (a: 0), current_color.SetRgba (a: 1)]));
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

			if (IsMouseInDrawingArea (this, color_circle_hue, absPos, out relPos)) {
				mouse_on_color_circle = true;
			} else

			if (IsMouseInDrawingArea (this, recentPaletteSwatches, absPos, out relPos)) {
				var recent_index = StatusBarColorPaletteWidget.GetSwatchAtLocation (relPos, new RectangleD(), true);

				if (recent_index >= 0) {
					current_color = PintaCore.Palette.CurrentPalette[recent_index];
					UpdateColorView ();
				}
			} else

			if (IsMouseInDrawingArea (this, paletteSwatches, absPos, out relPos)) {
				var index = StatusBarColorPaletteWidget.GetSwatchAtLocation (relPos, new RectangleD());

				if (index >= 0) {
					current_color = PintaCore.Palette.CurrentPalette[index];
					UpdateColorView ();
				}
			}

		};
		click_gesture.OnReleased += (_, e) => {
			mouse_on_color_circle = false;
		};
		AddController (click_gesture);

		var motion_controller = Gtk.EventControllerMotion.New ();
		motion_controller.OnMotion += (_, args) => {

			if (mouse_on_color_circle)
				SetColorFromCircle(new PointD (args.X, args.Y));
		};
		AddController (motion_controller);

		#endregion


		Gtk.Box mainVbox = new () { Spacing = spacing };
		mainVbox.SetOrientation (Gtk.Orientation.Vertical);

		topBox.Append (colorDisplayArea);
		topBox.Append (colorCircleBox);
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
		if (args.Row.GetIndex () == 0) {
			is_editing_primary_color = true;
			current_color = primary_color;
		} else {
			is_editing_primary_color = false;
			current_color = secondary_color;
		}
		color_circle_cursor.QueueDraw ();
		SetColorFromHsv ();
		RedrawValue ();
	}

	private void DrawCursor (Context g)
	{
		var loc = HsvToLocation (current_color.Hsv (), color_circle_radius);
		loc = new PointD (loc.X + color_circle_radius + color_circle_padding, loc.Y + color_circle_radius + color_circle_padding);

		g.FillRectangle (new RectangleD (loc.X - 5, loc.Y - 5, 10, 10), current_color);
		g.DrawRectangle (new RectangleD (loc.X - 5, loc.Y - 5, 10, 10), new Color (0, 0, 0), 4);
		g.DrawRectangle (new RectangleD (loc.X - 5, loc.Y - 5, 10, 10), new Color (1, 1, 1), 1);
	}

	private void DrawGradient (Context context, int width, int height, Color[] colors)
	{
		var pat = new LinearGradient (
			cps_padding_width,
			cps_padding_height,
			width - cps_padding_width,
			height - cps_padding_height);

		for (int i = 0; i < colors.Length; i++)
			pat.AddColorStop (i / (double)(colors.Length - 1), colors[i]);

		context.Rectangle (cps_padding_width,
			cps_padding_height,
			width - cps_padding_width * 2,
			height - cps_padding_height * 2);
		context.SetSource (pat);
		context.Fill ();
	}

	private void RedrawValue (bool force = false)
	{
		var val = current_color.Val ();
		if (!color_circle_show_value)
			val = 1;
		// If nothing has changed, do not re-render
		if (last_rendered_value != val || force) {
			last_rendered_value = val;
			color_circle_value.QueueDraw ();
		}
	}

	private void DrawValue (Context g)
	{
		var blackness = 1.0 - current_color.Val ();

		if (!color_circle_show_value)
			blackness = 0;

		g.Antialias = Antialias.None;
		g.FillEllipse (new RectangleD (color_circle_padding, color_circle_padding, color_circle_radius * 2 + 1, color_circle_radius * 2 + 1), new Color (0, 0, 0, blackness));


	}

	private void DrawColorDisplay (Context g, Color c)
	{
		int xy = color_display_border_thickness;
		int wh = color_display_size - color_display_border_thickness * 2;
		g.Antialias = Antialias.None;

		// make checker pattern
		if (c.A != 1) {
			g.FillRectangle (new RectangleD (xy, xy, wh, wh), new Color(1,1,1));
			g.FillRectangle (new RectangleD (xy, xy, wh / 2, wh / 2), new Color(.8,.8,.8));
			g.FillRectangle (new RectangleD (xy + wh / 2, xy + wh / 2, wh / 2, wh / 2), new Color(.8,.8,.8));
		}

		g.FillRectangle (new RectangleD (xy, xy, wh, wh), c);
		g.DrawRectangle (new RectangleD (xy, xy, wh, wh), new Color(0,0,0), color_display_border_thickness);
	}

	// INCREDIBLY inefficient!!!
	private void DrawColorCircle (Context g)
	{
		PointI center = new PointI (color_circle_radius, color_circle_radius);

		for (int y = 0; y <= color_circle_radius * 2; y++) {
			for (int x = 0; x <= color_circle_radius * 2; x++) {
				PointI pxl = new PointI (x, y);
				PointI vec = pxl - center;
				if (vec.Magnitude () <= color_circle_radius - 1) {
					var hue = (MathF.Atan2 (vec.Y, -vec.X) + MathF.PI) / (2f * MathF.PI) * 360f;

					var sat = Math.Min (vec.Magnitude () / color_circle_radius, 1);

					var hsv = new HsvColor ((int) hue, (int) (sat * 100), 100);
					var rgb = hsv.ToRgb ();
					g.DrawRectangle (new RectangleD (x + color_circle_padding, y + color_circle_padding, 1, 1), new Color (rgb.Red / 255.0, rgb.Green / 255.0, rgb.Blue / 255.0), 2);
					g.Fill ();
				}
			}
		}
	}

	private void UpdateColorView ()
	{
		RedrawValue ();
		SetColorFromHsv ();
	}

	private PointD HsvToLocation (Tuple<double, double, double> hsv, int radius)
	{
		var rad = hsv.Item1 * (Math.PI / 180.0);
		var mult = radius;
		var mag = hsv.Item2 * mult;
		var x = Math.Cos (rad) * mag;
		var y = Math.Sin (rad) * mag;
		return new PointD (x, -y);
	}

	void SetColorFromCircle (PointD point)
	{
		color_circle_hue.TranslateCoordinates (this, color_circle_padding, color_circle_padding, out var x, out var y);

		PointI centre = new PointI (color_circle_radius, color_circle_radius);
		PointI cursor = new PointI ((int)(point.X - x), (int)(point.Y - y));

		PointI vecCursor = cursor - centre;

		// Numbers from thin air!
		var hue = (MathF.Atan2 (vecCursor.Y, -vecCursor.X) + MathF.PI) / (2f * MathF.PI) * 360f;

		var sat = Math.Min(vecCursor.Magnitude () / 100.0, 1);

		current_color.SetHsv (hue: hue, saturation: sat);
		SetColorFromHsv ();
	}

	bool SetColorFromHsv ()
	{
		hue_cps.SetValue (current_color.Hue ());
		sat_cps.SetValue (current_color.Sat () * 100.0);
		val_cps.SetValue (current_color.Val () * 100.0);

		r_cps.SetValue (current_color.R * 255.0);
		g_cps.SetValue (current_color.G * 255.0);
		b_cps.SetValue (current_color.B * 255.0);
		a_cps.SetValue (current_color.A * 255.0);



		if(GetFocus ()?.Parent != hex_entry)
			hex_entry.SetText (current_color.ToHex ());

		if (is_editing_primary_color) {
			primary_color = current_color;
			color_display_primary.QueueDraw ();
		} else {
			secondary_color = current_color;
			color_display_secondary.QueueDraw ();
		}

		color_circle_cursor.QueueDraw ();
		return true;
	}
}
