# Recadre une image de bâtiment (fond transparent) pour qu'elle ait EXACTEMENT la même
# emprise que l'image de référence (hub_lvl1.png) : même canevas, même taille, même centre.
# Nettoie aussi les pixels quasi transparents (halo / ombre résiduelle du détourage).
#
#   normalize.ps1 -In cut/x.png -Out out/x.png -Ref ../../Assets/Resources/UI/WareHouse/hub_lvl1.png
param(
  [Parameter(Mandatory)][string]$In,
  [Parameter(Mandatory)][string]$Out,
  [Parameter(Mandatory)][string]$Ref,
  [switch]$Flip,                    # miroir horizontal (façade tournée du mauvais côté)
  [double]$MaxHeightFactor = 1.0   # 1.0 = jamais plus haut ni plus large que l'emprise du hangar de référence
)

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class BuildingNormalizer
{
    const int AlphaMin = 48;   // en dessous : considéré comme du vide

    static Bitmap Load32(string path)
    {
        using (var raw = new Bitmap(path))
        {
            var bmp = new Bitmap(raw.Width, raw.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp)) g.DrawImage(raw, 0, 0, raw.Width, raw.Height);
            return bmp;
        }
    }

    // Boîte englobante des pixels visibles. Si clean : remet à 0 l'alpha des pixels trop
    // transparents ET supprime les morceaux isolés (bornes flottantes, ombre résiduelle…) :
    // on ne garde que les ensembles de pixels connectés d'au moins 4 % du plus gros.
    static Rectangle VisibleBounds(Bitmap bmp, bool clean)
    {
        int w = bmp.Width, h = bmp.Height;
        var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        int stride = Math.Abs(data.Stride);
        var px = new byte[stride * h];
        Marshal.Copy(data.Scan0, px, 0, px.Length);

        var label = new int[w * h];          // 0 = vide / non étiqueté
        var sizes = new System.Collections.Generic.List<int> { 0 };
        if (clean)
        {
            var stack = new System.Collections.Generic.Stack<int>();
            for (int s = 0; s < w * h; s++)
            {
                if (label[s] != 0 || px[(s / w) * stride + (s % w) * 4 + 3] <= AlphaMin) continue;
                int id = sizes.Count, count = 0;
                label[s] = id; stack.Push(s);
                while (stack.Count > 0)
                {
                    int p = stack.Pop(); count++;
                    int px0 = p % w, py0 = p / w;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = px0 + dx, ny = py0 + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            int n = ny * w + nx;
                            if (label[n] != 0 || px[ny * stride + nx * 4 + 3] <= AlphaMin) continue;
                            label[n] = id; stack.Push(n);
                        }
                }
                sizes.Add(count);
            }
        }
        int biggest = 0; foreach (var c in sizes) if (c > biggest) biggest = c;
        int minKeep = (int)(biggest * 0.04);

        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int o = y * stride + x * 4;
                bool visible = px[o + 3] > AlphaMin && (!clean || sizes[label[y * w + x]] >= minKeep);
                if (!visible) { if (clean) { px[o] = px[o + 1] = px[o + 2] = px[o + 3] = 0; } continue; }
                if (x < minX) minX = x; if (x > maxX) maxX = x;
                if (y < minY) minY = y; if (y > maxY) maxY = y;
            }
        if (clean) Marshal.Copy(px, 0, data.Scan0, px.Length);
        bmp.UnlockBits(data);
        if (maxX < 0) throw new Exception("Image entièrement transparente");
        return new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    public static string Run(string inPath, string outPath, string refPath, double maxHeightFactor, bool flip)
    {
        Rectangle refBox; int cw, ch;
        using (var r = Load32(refPath)) { refBox = VisibleBounds(r, false); cw = r.Width; ch = r.Height; }

        using (var src = Load32(inPath))
        {
            if (flip) src.RotateFlip(RotateFlipType.RotateNoneFlipX);   // façade côté droit, comme le hangar de référence
            var box = VisibleBounds(src, true);
            double boxW = refBox.Width, boxH = refBox.Height * maxHeightFactor;
            double scale = Math.Min(boxW / box.Width, boxH / box.Height);
            double dw = box.Width * scale, dh = box.Height * scale;

            double cx = refBox.X + refBox.Width / 2.0, cy = refBox.Y + refBox.Height / 2.0;
            var dest = new RectangleF((float)(cx - dw / 2), (float)(cy - dh / 2), (float)dw, (float)dh);

            using (var canvas = new Bitmap(cw, ch, PixelFormat.Format32bppPArgb))
            using (var g = Graphics.FromImage(canvas))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                using (var ia = new ImageAttributes())
                {
                    ia.SetWrapMode(WrapMode.TileFlipXY);   // évite le liseré sur les bords du recadrage
                    g.DrawImage(src, Rectangle.Round(dest), box.X, box.Y, box.Width, box.Height, GraphicsUnit.Pixel, ia);
                }
                canvas.Save(outPath, ImageFormat.Png);
            }
            return string.Format("{0}x{1} -> {2:0}x{3:0} sur canevas {4}x{5} (référence {6}x{7})",
                box.Width, box.Height, dw, dh, cw, ch, refBox.Width, refBox.Height);
        }
    }
}
"@

try {
  $inP  = (Resolve-Path -LiteralPath $In).Path
  $refP = (Resolve-Path -LiteralPath $Ref).Path
  # Chemin de sortie : absolu tel quel, sinon relatif au dossier courant.
  $outP = if ([System.IO.Path]::IsPathRooted($Out)) { $Out } else { Join-Path (Get-Location).Path $Out }
  [BuildingNormalizer]::Run($inP, [System.IO.Path]::GetFullPath($outP), $refP, $MaxHeightFactor, [bool]$Flip)
} catch {
  [Console]::Error.WriteLine($_.Exception.Message)
  exit 1
}
