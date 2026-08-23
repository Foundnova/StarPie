using System.Windows.Controls;
using System.Windows.Media;

namespace WinPieGestures
{
    /// <summary>
    /// Clean Sectors Style: Floating geometric cards with precise optical gaps and modern emerald/mint accents.
    /// </summary>
    public class CleanSectorsRenderer : BaseStyleRenderer
    {
        protected override void GetDefaultColors(string theme, out string sectorBgHex, out string sectorBorderHex, out string highlightBgHex, out string highlightBorderHex, out string textHex)
        {
            if (theme == "Light")
            {
                sectorBgHex = "#F5FFFFFF";
                sectorBorderHex = "#30CBD5E1";
                highlightBgHex = "#FF059669"; // Emerald 600
                highlightBorderHex = "#FF34D399";
                textHex = "#FF0F172A";
            }
            else
            {
                sectorBgHex = "#F00F172A";     // Slate 900
                sectorBorderHex = "#30475569"; // Slate border
                highlightBgHex = "#FF10B981";  // Emerald 500
                highlightBorderHex = "#FF6EE7B7";
                textHex = "#FFF8FAFC";
            }
        }

        protected override void PostInitialize()
        {
            BorderThickness = 1.2;
            HighlightBorderThickness = 2.0;
        }

        public override void RenderDecorations(Canvas canvas, Grid coreGrid, double cx, double cy, double wheelRadius, double coreRadius, int insertIndex)
        {
            // Clean minimal floating style without noisy clutter
        }
    }
}
