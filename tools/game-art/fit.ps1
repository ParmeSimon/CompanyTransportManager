# Cadre une image (fond transparent ou non) sur un canevas fixe W x H :
# rogne au contenu visible (sans les miettes isolées), agrandit/réduit pour tenir dans le
# canevas avec une marge, centre horizontalement, et pose le sujet sur une même ligne de sol (-Bottom).
param(
  [Parameter(Mandatory)][string]$In, [Parameter(Mandatory)][string]$Out,
  [int]$W = 512, [int]$H = 384, [double]$Pad = 0.06, [switch]$Bottom, [switch]$Flip
)

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class GameArtFit
{
    const int AlphaMin = 40;

    public static string Run(string inPath, string outPath, int cw, int ch, double pad, bool bottom, bool flip)
    {
        Bitmap src;
        using (var raw = new Bitmap(inPath))
        {
            src = new Bitmap(raw.Width, raw.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(src)) g.DrawImage(raw, 0, 0, raw.Width, raw.Height);
        }
        if (flip) src.RotateFlip(RotateFlipType.RotateNoneFlipX);   // tourné du mauvais côté
        int w = src.Width, h = src.Height;
        var data = src.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        int stride = data.Stride;
        var px = new byte[stride * h];
        Marshal.Copy(data.Scan0, px, 0, px.Length);

        // Composantes connexes : on garde celles d'au moins 4 % de la plus grande.
        var label = new int[w * h]; var sizes = new List<int> { 0 }; var stack = new Stack<int>();
        for (int s = 0; s < w * h; s++)
        {
            if (label[s] != 0 || px[(s / w) * stride + (s % w) * 4 + 3] <= AlphaMin) continue;
            int id = sizes.Count, count = 0; label[s] = id; stack.Push(s);
            while (stack.Count > 0)
            {
                int p = stack.Pop(); count++; int x0 = p % w, y0 = p / w;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x0 + dx, ny = y0 + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int n = ny * w + nx;
                    if (label[n] != 0 || px[ny * stride + nx * 4 + 3] <= AlphaMin) continue;
                    label[n] = id; stack.Push(n);
                }
            }
            sizes.Add(count);
        }
        int biggest = 0; foreach (var c in sizes) if (c > biggest) biggest = c;
        int keep = (int)(biggest * 0.04);
        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            int o = y * stride + x * 4;
            bool vis = px[o + 3] > AlphaMin && sizes[label[y * w + x]] >= keep;
            if (!vis) { px[o] = px[o + 1] = px[o + 2] = px[o + 3] = 0; continue; }
            if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y;
        }
        Marshal.Copy(px, 0, data.Scan0, px.Length);
        src.UnlockBits(data);
        if (maxX < 0) throw new Exception("image vide");

        int bw = maxX - minX + 1, bh = maxY - minY + 1;
        double availW = cw * (1 - 2 * pad), availH = ch * (1 - 2 * pad);
        double sc = Math.Min(availW / bw, availH / bh);
        double dw = bw * sc, dh = bh * sc;
        double dx0 = (cw - dw) / 2, dy0 = bottom ? ch * (1 - pad) - dh : (ch - dh) / 2;

        using (var canvas = new Bitmap(cw, ch, PixelFormat.Format32bppPArgb))
        using (var g = Graphics.FromImage(canvas))
        using (var ia = new ImageAttributes())
        {
            g.Clear(Color.Transparent);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            ia.SetWrapMode(WrapMode.TileFlipXY);
            g.DrawImage(src, new Rectangle((int)Math.Round(dx0), (int)Math.Round(dy0), (int)Math.Round(dw), (int)Math.Round(dh)),
                        minX, minY, bw, bh, GraphicsUnit.Pixel, ia);
            canvas.Save(outPath, ImageFormat.Png);
        }
        src.Dispose();
        return string.Format("{0}x{1} -> {2:0}x{3:0} sur {4}x{5}", bw, bh, dw, dh, cw, ch);
    }
}
"@

try {
  [GameArtFit]::Run((Resolve-Path -LiteralPath $In).Path, [System.IO.Path]::GetFullPath($Out), $W, $H, $Pad, [bool]$Bottom, [bool]$Flip)
} catch { [Console]::Error.WriteLine($_.Exception.Message); exit 1 }
