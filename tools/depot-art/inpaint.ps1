# Outils image pour intégrer un bâtiment DANS la carte du dépôt (pas « une image sur une image »).
#
#   inpaint.ps1 -Mode crop  -Map depotMap.png -Cx 0.534 -Cy 0.572 -W 0.36 -Aspect 1.2496 -Scale 3 -Out crop.png
#       Découpe la zone de l'emplacement (coordonnées normalisées, y depuis le BAS comme dans Unity)
#       et l'agrandit (Scale) pour la génération.
#   inpaint.ps1 -Mode patch -Gen gen.png -Crop crop.png -Out hub_lvl1.png -Feather 0.14 -Scale 2
#       Remet l'image générée à la taille de l'emplacement (× Scale pour la netteté) et fond ses bords
#       (alpha dégradé) pour qu'elle se raccorde au parking autour.
param(
  [Parameter(Mandatory)][ValidateSet('crop','patch')][string]$Mode,
  [string]$Map, [double]$Cx, [double]$Cy, [double]$W, [double]$Aspect = 1.2496, [double]$Scale = 3,
  [string]$Gen, [string]$Crop, [double]$Feather = 0.14, [double]$SlotW = 0, [double]$SlotH = 0,
  [Parameter(Mandatory)][string]$Out
)

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class DepotInpaint
{
    static Bitmap Load32(string path)
    {
        using (var raw = new Bitmap(path))
        {
            var bmp = new Bitmap(raw.Width, raw.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp)) g.DrawImage(raw, 0, 0, raw.Width, raw.Height);
            return bmp;
        }
    }

    static Bitmap Resize(Bitmap src, int w, int h)
    {
        var dst = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(dst))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            using (var ia = new ImageAttributes())
            {
                ia.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(src, new Rectangle(0, 0, w, h), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, ia);
            }
        }
        return dst;
    }

    // Rectangle de l'emplacement en pixels de la carte (y Unity depuis le bas → y image depuis le haut).
    public static string Crop(string map, double cx, double cy, double wFrac, double aspect, double scale, string outPath)
    {
        using (var m = Load32(map))
        {
            double w = wFrac * m.Width, h = w / aspect;
            double x = cx * m.Width - w / 2, y = (1 - cy) * m.Height - h / 2;
            var rect = new Rectangle((int)Math.Round(x), (int)Math.Round(y), (int)Math.Round(w), (int)Math.Round(h));
            // L'emplacement peut dépasser le bord de la carte (cuve, à droite) : on recopie la zone
            // en miroir au-delà du bord au lieu de Clone(), qui échoue hors de l'image.
            using (var c = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb))
            {
                using (var gc = Graphics.FromImage(c))
                using (var ia = new ImageAttributes())
                {
                    ia.SetWrapMode(WrapMode.TileFlipXY);
                    gc.DrawImage(m, new Rectangle(0, 0, rect.Width, rect.Height), rect.X, rect.Y, rect.Width, rect.Height, GraphicsUnit.Pixel, ia);
                }
                // Taille multiple de 64 (exigence des modèles) et ~3x pour le détail.
                int ow = Math.Max(64, (int)(Math.Round(rect.Width * scale / 64.0) * 64));
                int oh = Math.Max(64, (int)(Math.Round(rect.Height * scale / 64.0) * 64));
                using (var big = Resize(c, ow, oh))
                {
                    // Sans alpha (JPEG/PNG RGB) : le modèle refuse la transparence.
                    using (var rgb = new Bitmap(ow, oh, PixelFormat.Format24bppRgb))
                    {
                        using (var g = Graphics.FromImage(rgb)) g.DrawImage(big, 0, 0, ow, oh);
                        rgb.Save(outPath, ImageFormat.Png);
                    }
                }
            }
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "emplacement {0:0.###}x{1:0.###} px a ({2},{3}) -> {4}", w, h, rect.X, rect.Y, outPath);
        }
    }

    // Image générée → patch à la taille de l'emplacement × scale, bords fondus (alpha).
    public static string Patch(string gen, string crop, string outPath, double feather, double scale, double slotW, double slotH)
    {
        int w = (int)Math.Round(slotW * scale), h = (int)Math.Round(slotH * scale);
        using (var g0 = Load32(gen))
        using (var p = Resize(g0, w, h))
        using (var c0 = Load32(crop))
        using (var c = Resize(c0, w, h))
        {
            var data = p.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            var px = new byte[Math.Abs(data.Stride) * h];
            Marshal.Copy(data.Scan0, px, 0, px.Length);
            var cdata = c.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var cpx = new byte[Math.Abs(cdata.Stride) * h];
            Marshal.Copy(cdata.Scan0, cpx, 0, cpx.Length);
            c.UnlockBits(cdata);

            // Étalonnage des couleurs : moyenne / écart-type par canal mesurés sur la BORDURE
            // (là où il n'y a que du parking), puis on aligne l'image générée sur la carte.
            double[] sg = new double[3], sg2 = new double[3], sc = new double[3], sc2 = new double[3]; long n = 0;
            int bx = (int)(w * 0.18), by = (int)(h * 0.18);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (x > bx && x < w - bx && y > by && y < h - by) continue;
                    int o = y * data.Stride + x * 4;
                    for (int k = 0; k < 3; k++) { double a = px[o + k], b = cpx[o + k]; sg[k] += a; sg2[k] += a * a; sc[k] += b; sc2[k] += b * b; }
                    n++;
                }
            double[] mg = new double[3], dg = new double[3], mc = new double[3], dc = new double[3];
            for (int k = 0; k < 3; k++)
            {
                mg[k] = sg[k] / n; dg[k] = Math.Sqrt(Math.Max(1, sg2[k] / n - mg[k] * mg[k]));
                mc[k] = sc[k] / n; dc[k] = Math.Sqrt(Math.Max(1, sc2[k] / n - mc[k] * mc[k]));
            }
            for (int i = 0; i < px.Length; i += 4)
                for (int k = 0; k < 3; k++)
                {
                    double v = (px[i + k] - mg[k]) * Math.Min(2.0, dc[k] / dg[k]) + mc[k];
                    px[i + k] = (byte)Math.Max(0, Math.Min(255, Math.Round(v)));
                }

            double fx = Math.Max(1, w * feather), fy = Math.Max(1, h * feather);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // distance au bord, normalisée par la largeur du fondu ; lissée (smoothstep)
                    double dx = Math.Min(x, w - 1 - x) / fx, dy = Math.Min(y, h - 1 - y) / fy;
                    double t = Math.Min(1, Math.Min(dx, dy));
                    t = t * t * (3 - 2 * t);
                    int o = y * data.Stride + x * 4;
                    px[o + 3] = (byte)Math.Round(px[o + 3] * t);
                }
            Marshal.Copy(px, 0, data.Scan0, px.Length);
            p.UnlockBits(data);
            p.Save(outPath, ImageFormat.Png);
        }
        return string.Format("patch {0}x{1} -> {2}", w, h, outPath);
    }
}
"@

try {
  if ($Mode -eq 'crop') {
    [DepotInpaint]::Crop((Resolve-Path $Map).Path, $Cx, $Cy, $W, $Aspect, $Scale, [System.IO.Path]::GetFullPath($Out))
  } else {
    # Taille EXACTE de l'emplacement (px de la carte), donnée par le mode crop : le patch garde
    # ainsi les proportions de l'emplacement et tombe pile au bon endroit en jeu.
    if ($SlotW -le 0 -or $SlotH -le 0) { throw "SlotW / SlotH requis (taille de l'emplacement en px)" }
    [DepotInpaint]::Patch((Resolve-Path $Gen).Path, (Resolve-Path $Crop).Path, [System.IO.Path]::GetFullPath($Out), $Feather, $Scale, $SlotW, $SlotH)
  }
} catch {
  [Console]::Error.WriteLine($_.Exception.Message); exit 1
}
