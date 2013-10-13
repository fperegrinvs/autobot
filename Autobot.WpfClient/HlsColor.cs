namespace Autobot.WpfClient
{
    using System;
    using System.Windows.Media;

    /// <include file='doc\ControlPaint.uex' path='docs/doc[@for="ControlPaint.HLSColor"]/*' />
    /// <devdoc>
    ///     Logic copied from Win2K sources to copy the lightening and
    ///     darkening of colors.
    /// </devdoc>
    public struct HlsColor
    {
        private const int ShadowAdj = -333;
        private const int HilightAdj = 500;

        private const int Range = 240;
        private const int HLSMax = Range;
        private const int RGBMax = 255;
        private const int Undefined = HLSMax * 2 / 3;

        private int hue;
        private int saturation;
        private int luminosity;

        /// <include file='doc\ControlPaint.uex' path='docs/doc[@for="ControlPaint.HLSColor.HLSColor"]/*' />
        /// <devdoc>
        /// </devdoc>
        public HlsColor(Color color)
        {
            int r = color.R;
            int g = color.G;
            int b = color.B;
            int max, min;        /* max and min RGB values */
            int sum, dif;
            int Rdelta, Gdelta, Bdelta;  /* intermediate value: % of spread from max */

            /* calculate lightness */
            max = Math.Max(Math.Max(r, g), b);
            min = Math.Min(Math.Min(r, g), b);
            sum = max + min;

            this.luminosity = (((sum * HLSMax) + RGBMax) / (2 * RGBMax));

            dif = max - min;
            if (dif == 0)
            {       /* r=g=b --> achromatic case */
                this.saturation = 0;                         /* saturation */
                this.hue = Undefined;                 /* hue */
            }
            else
            {                           /* chromatic case */
                /* saturation */
                if (this.luminosity <= (HLSMax / 2))
                    this.saturation = (int)(((dif * (int)HLSMax) + (sum / 2)) / sum);
                else
                    this.saturation = (int)((int)((dif * (int)HLSMax) + (int)((2 * RGBMax - sum) / 2))
                        / (2 * RGBMax - sum));
                /* hue */
                Rdelta = (int)((((max - r) * (int)(HLSMax / 6)) + (dif / 2)) / dif);
                Gdelta = (int)((((max - g) * (int)(HLSMax / 6)) + (dif / 2)) / dif);
                Bdelta = (int)((((max - b) * (int)(HLSMax / 6)) + (dif / 2)) / dif);

                if ((int)r == max)
                    this.hue = Bdelta - Gdelta;
                else if ((int)g == max)
                    this.hue = (HLSMax / 3) + Rdelta - Bdelta;
                else /* B == cMax */
                    this.hue = ((2 * HLSMax) / 3) + Gdelta - Rdelta;

                if (this.hue < 0)
                    this.hue += HLSMax;
                if (this.hue > HLSMax)
                    this.hue -= HLSMax;
            }
        }

        /// <include file='doc\ControlPaint.uex' path='docs/doc[@for="ControlPaint.HLSColor.Hue"]/*' />
        /// <devdoc>
        /// </devdoc>
        public int Hue
        {
            get
            {
                return this.hue;
            }
        }

        /// <include file='doc\ControlPaint.uex' path='docs/doc[@for="ControlPaint.HLSColor.Luminosity"]/*' />
        /// <devdoc>
        /// </devdoc>
        public int Luminosity
        {
            get
            {
                return this.luminosity;
            }
        }

        /// <include file='doc\ControlPaint.uex' path='docs/doc[@for="ControlPaint.HLSColor.Saturation"]/*' />
        /// <devdoc>
        /// </devdoc>
        public int Saturation
        {
            get
            {
                return this.saturation;
            }
        }

        public Color Darker(float percDarker)
        {
            int oneLum = 0;
            int zeroLum = this.NewLuma(ShadowAdj, true);
            return ColorFromHLS(this.hue, zeroLum - (int)((zeroLum - oneLum) * percDarker), this.saturation);
        }

        public static bool operator ==(HlsColor a, HlsColor b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(HlsColor a, HlsColor b)
        {
            return !a.Equals(b);
        }

        public override bool Equals(object o)
        {
            if (!(o is HlsColor))
            {
                return false;
            }

            HlsColor c = (HlsColor)o;
            return this.hue == c.hue &&
                this.saturation == c.saturation &&
                this.luminosity == c.luminosity;
        }

        public override int GetHashCode()
        {
            return this.hue << 6 | this.saturation << 2 | this.luminosity;
        }

        public Color Lighter(float percLighter)
        {
            int zeroLum = this.luminosity;
            int oneLum = this.NewLuma(HilightAdj, true);
            return ColorFromHLS(this.hue, zeroLum + (int)((oneLum - zeroLum) * percLighter), this.saturation);
        }

        private int NewLuma(int n, bool scale)
        {
            return NewLuma(this.luminosity, n, scale);
        }

        private static int NewLuma(int luminosity, int n, bool scale)
        {
            if (n == 0)
                return luminosity;

            if (scale)
            {
                if (n > 0)
                {
                    return (int)(((int)luminosity * (1000 - n) + (Range + 1L) * n) / 1000);
                }
                else
                {
                    return (int)(((int)luminosity * (n + 1000)) / 1000);
                }
            }

            int newLum = luminosity;
            newLum += (int)((long)n * Range / 1000);

            if (newLum < 0)
                newLum = 0;
            if (newLum > HLSMax)
                newLum = HLSMax;

            return newLum;
        }

        /// <include file='doc\ControlPaint.uex' path='docs/doc[@for="ControlPaint.HLSColor.ColorFromHLS"]/*' />
        /// <devdoc>
        /// </devdoc>
        public static Color ColorFromHLS(int hue, int luminosity, int saturation)
        {
            byte r, g, b;                      /* RGB component values */
            int magic1, magic2;       /* calculated magic numbers (really!) */

            if (saturation == 0)
            {                /* achromatic case */
                r = g = b = (byte)((luminosity * RGBMax) / HLSMax);
                if (hue != Undefined)
                {
                    /* ERROR */
                }
            }
            else
            {                         /* chromatic case */
                /* set up magic numbers */
                if (luminosity <= (HLSMax / 2))
                    magic2 = (int)((luminosity * ((int)HLSMax + saturation) + (HLSMax / 2)) / HLSMax);
                else
                    magic2 = luminosity + saturation - (int)(((luminosity * saturation) + (int)(HLSMax / 2)) / HLSMax);
                magic1 = 2 * luminosity - magic2;

                /* get RGB, change units from HLSMax to RGBMax */
                r = (byte)(((HueToRGB(magic1, magic2, (int)(hue + (int)(HLSMax / 3))) * (int)RGBMax + (HLSMax / 2))) / (int)HLSMax);
                g = (byte)(((HueToRGB(magic1, magic2, hue) * (int)RGBMax + (HLSMax / 2))) / HLSMax);
                b = (byte)(((HueToRGB(magic1, magic2, (int)(hue - (int)(HLSMax / 3))) * (int)RGBMax + (HLSMax / 2))) / (int)HLSMax);
            }
            return Color.FromRgb(r, g, b);
        }

        /// <include file='doc\ControlPaint.uex' path='docs/doc[@for="ControlPaint.HLSColor.HueToRGB"]/*' />
        /// <devdoc>
        /// </devdoc>
        private static int HueToRGB(int n1, int n2, int hue)
        {
            /* range check: note values passed add/subtract thirds of range */

            /* The following is redundant for WORD (unsigned int) */
            if (hue < 0)
                hue += HLSMax;

            if (hue > HLSMax)
                hue -= HLSMax;

            /* return r,g, or b value from this tridrant */
            if (hue < (HLSMax / 6))
                return (n1 + (((n2 - n1) * hue + (HLSMax / 12)) / (HLSMax / 6)));
            if (hue < (HLSMax / 2))
                return (n2);
            if (hue < ((HLSMax * 2) / 3))
                return (n1 + (((n2 - n1) * (((HLSMax * 2) / 3) - hue) + (HLSMax / 12)) / (HLSMax / 6)));
            else
                return (n1);

        }

        public override string ToString()
        {
            return this.hue + ", " + this.luminosity + ", " + this.saturation;
        }
    }
}