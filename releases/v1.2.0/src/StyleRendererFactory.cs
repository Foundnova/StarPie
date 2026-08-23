namespace WinPieGestures
{
    public static class StyleRendererFactory
    {
        /// <summary>
        /// Instantiates the appropriate style renderer for the given style name.
        /// </summary>
        public static IRadialStyleRenderer CreateRenderer(string style)
        {
            if (string.IsNullOrEmpty(style))
            {
                return new ClassicRingRenderer();
            }

            switch (style.Trim())
            {
                case "CatPaw":
                    return new CatPawRenderer();
                case "Mechanical":
                    return new MechanicalRenderer();
                case "Glassmorphism":
                    return new GlassmorphismRenderer();
                case "NeonGlow":
                case "Tech":
                    return new NeonGlowRenderer();
                case "CleanSectors":
                    return new CleanSectorsRenderer();
                case "ClassicRing":
                default:
                    return new ClassicRingRenderer();
            }
        }
    }
}
