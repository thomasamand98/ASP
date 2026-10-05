using System.Globalization;

namespace Horizon.Web.Common;

// Calculs du graphique à barres du tableau de bord (voir ChiffreAffairesChart) :
// valeurs d'axe « rondes », position des barres, choix des étiquettes qui
// tiennent côte à côte. Reprend les règles de la bibliothèque de graphiques
// du front d'origine (Recharts) pour que le dessin soit identique.
public static class ChartMath
{
    // Graduations d'un axe de valeurs de `min` à `max` : `tickCount` valeurs
    // à pas régulier « facile à lire » (100, 150, 200...), pouvant déborder
    // de l'intervalle.
    public static List<decimal> NiceTicks(decimal min, decimal max, int tickCount = 5)
    {
        var count = Math.Max(tickCount, 2);
        if (min > max) (min, max) = (max, min);
        if (min == max) return SingleValueTicks(min, count);

        var (step, tickMin, tickMax) = CalculateStep(min, max, count, 0);
        var values = new List<decimal>();
        if (step <= 0) return values;
        var end = tickMax + 0.1m * step;
        for (var value = tickMin; value < end && values.Count < 1000; value += step) values.Add(value);
        return values;
    }

    private static List<decimal> SingleValueTicks(decimal value, int count)
    {
        var middle = value;
        var step = 1m;
        if (value != Math.Truncate(value))
        {
            var abs = Math.Abs(value);
            if (abs < 1)
            {
                step = Pow10(DigitCount(value) - 1);
                middle = Math.Floor(middle / step) * step;
            }
            else
            {
                middle = Math.Floor(value);
            }
        }
        else if (value == 0)
        {
            middle = (decimal)Math.Floor((count - 1) / 2d);
        }
        var middleIndex = (int)Math.Floor((count - 1) / 2d);
        return [.. Enumerable.Range(0, count).Select(i => middle + (i - middleIndex) * step)];
    }

    private static (decimal Step, decimal TickMin, decimal TickMax) CalculateStep(decimal min, decimal max, int tickCount, int correction)
    {
        var step = AdaptiveStep((max - min) / (tickCount - 1), correction);
        decimal middle;
        if (min <= 0 && max >= 0)
        {
            middle = 0;
        }
        else
        {
            middle = (min + max) / 2;
            middle -= middle % step;
        }
        var below = (int)Math.Ceiling((middle - min) / step);
        var up = (int)Math.Ceiling((max - middle) / step);
        var scale = below + up + 1;
        if (scale > tickCount) return CalculateStep(min, max, tickCount, correction + 1);
        if (scale < tickCount)
        {
            if (max > 0) up += tickCount - scale;
            else below += tickCount - scale;
        }
        return (step, middle - below * step, middle + up * step);
    }

    // Pas « facile à comprendre » entre deux graduations : multiple de 0,05
    // de la puissance de dix qui dépasse le pas brut.
    private static decimal AdaptiveStep(decimal roughStep, int correction)
    {
        if (roughStep <= 0) return 0;
        var digits = DigitCount(roughStep);
        var magnitude = Pow10(digits);
        var ratio = roughStep / magnitude;
        var scale = digits != 1 ? 0.05m : 0.1m;
        return (Math.Ceiling(ratio / scale) + correction) * scale * magnitude;
    }

    private static int DigitCount(decimal value) => value == 0 ? 1 : (int)Math.Floor(Math.Log10((double)Math.Abs(value))) + 1;

    private static decimal Pow10(int exponent)
    {
        var result = 1m;
        for (var i = 0; i < Math.Abs(exponent); i++) result = exponent > 0 ? result * 10 : result / 10;
        return result;
    }

    // Position d'une barre dans sa catégorie (largeur `band`) : `count` barres
    // côte à côte séparées de `gap`, marges `categoryGap` (part de la
    // catégorie) de chaque côté, largeur plafonnée à `maxSize`.
    public static (double Offset, double Size) BarPosition(double band, int count, int index, double categoryGap, double gap, double maxSize)
    {
        var margin = band * categoryGap;
        var realGap = band - 2 * margin - (count - 1) * gap <= 0 ? 0 : gap;
        var original = (band - 2 * margin - (count - 1) * realGap) / count;
        if (original > 1) original = Math.Floor(original + 0.5);
        var size = Math.Min(original, maxSize);
        return (margin + count * (original - size) / 2 + (size + realGap) * index, size);
    }

    // Étiquettes d'axe horizontal à afficher : la première et la dernière
    // toujours (si elles tiennent), les autres tant qu'elles ne chevauchent
    // pas la précédente (écart minimal `minGap`).
    public static List<int> VisibleXTicks(IReadOnlyList<double> centers, IReadOnlyList<double> widths, double start, double end, double minGap)
    {
        var count = centers.Count;
        var shown = new HashSet<int>();
        if (count == 0) return [];
        var coords = centers.ToArray();

        var tailGap = coords[^1] + widths[^1] / 2 - end;
        coords[^1] = tailGap > 0 ? coords[^1] - tailGap : coords[^1];
        if (Fits(coords[^1], widths[^1], start, end))
        {
            end = coords[^1] - (widths[^1] / 2 + minGap);
            shown.Add(count - 1);
        }

        for (var i = 0; i < count - 1; i++)
        {
            if (i == 0)
            {
                var gap = coords[0] - widths[0] / 2 - start;
                coords[0] = gap < 0 ? coords[0] - gap : coords[0];
            }
            if (Fits(coords[i], widths[i], start, end))
            {
                start = coords[i] + (widths[i] / 2 + minGap);
                shown.Add(i);
            }
        }
        return [.. shown.Order()];
    }

    private static bool Fits(double position, double size, double start, double end) =>
        position >= start && position <= end && position - size / 2 - start >= 0 && position + size / 2 - end <= 0;

    // Nombre → attribut SVG (point décimal, pas d'exposant).
    public static string Svg(double value) => Math.Round(value, 4).ToString("0.####", CultureInfo.InvariantCulture);
}
