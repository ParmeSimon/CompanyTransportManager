# Reprojette un sol vu de dessus en vue ISOMÉTRIQUE (rotation 45°, écrasement vertical ×0,5),
# pour qu'il ait la même orientation que les bâtiments. La sortie est entièrement couverte :
# on agrandit jusqu'à ce que le losange déborde du cadre (les bords du terrain sont rognés).
#   iso.ps1 -In out/depotMap.png -Out out/depotMap_iso.png -W 1920 -H 1080 -Zoom 1.0
param(
  [Parameter(Mandatory)][string]$In,
  [Parameter(Mandatory)][string]$Out,
  [int]$W = 1920,
  [int]$H = 1080,
  [double]$Zoom = 1.0   # >1 : plus serré (on voit moins les bords du terrain)
)

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public static class IsoProjector
{
    public static string Run(string inPath, string outPath, int W, int H, double zoom)
    {
        using (var src = new Bitmap(inPath))
        using (var dst = new Bitmap(W, H, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(dst))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.FromArgb(255, 40, 42, 46));

            double sw = src.Width, sh = src.Height;
            // Plus petite échelle s telle que les 4 coins du cadre de sortie tombent dans l'image projetée.
            // Inverse d'un point (x,y) du cadre (centré) : y' = 2y ; rotation -45° ; division par s.
            double s = 0;
            double[,] corners = { { -W / 2.0, -H / 2.0 }, { W / 2.0, -H / 2.0 }, { W / 2.0, H / 2.0 }, { -W / 2.0, H / 2.0 } };
            double c = Math.Cos(-Math.PI / 4), sn = Math.Sin(-Math.PI / 4);
            for (int i = 0; i < 4; i++)
            {
                double x = corners[i, 0], y = corners[i, 1] * 2.0;
                double rx = x * c - y * sn, ry = x * sn + y * c;
                s = Math.Max(s, Math.Max(Math.Abs(rx) / (sw / 2.0), Math.Abs(ry) / (sh / 2.0)));
            }
            s *= zoom;

            var m = new Matrix();
            m.Translate(W / 2f, H / 2f);
            m.Scale(1f, 0.5f);
            m.Rotate(45f);
            m.Scale((float)s, (float)s);
            m.Translate((float)(-sw / 2), (float)(-sh / 2));
            g.Transform = m;
            g.DrawImage(src, 0, 0, src.Width, src.Height);
            dst.Save(outPath, ImageFormat.Png);
            return string.Format("{0}x{1} -> iso {2}x{3} (echelle {4:0.00})", src.Width, src.Height, W, H, s);
        }
    }
}
"@

$resolve = { param($p) if ([System.IO.Path]::IsPathRooted($p)) { $p } else { Join-Path (Get-Location) $p } }
[IsoProjector]::Run((& $resolve $In), (& $resolve $Out), $W, $H, $Zoom)
