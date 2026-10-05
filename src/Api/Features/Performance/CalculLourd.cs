using System.Diagnostics;

namespace Api.Features.Performance;

public record EtapeCalcul(string nom, double dureeMs, double resultat);

public record ResultatCalcul(DateTime debut, DateTime fin, double dureeTotaleMs, List<EtapeCalcul> etapes);

public record TaillesCalcul(int Crible, int Matrice, int Tri, long Hachage);

// Calcul lourd de référence (POST /performance/calcul) : une charge de
// travail fixe, sans durée imposée, pour comparer la vitesse de calcul des
// deux backends. Le même algorithme, avec les mêmes tailles, est écrit en
// TypeScript dans backend/src/performance/calcul-lourd.ts : les résultats de
// chaque étape doivent être identiques des deux côtés, seules les durées
// diffèrent. Un seul fil d'exécution, aucune base de données.
public static class CalculLourd
{
    public static readonly TaillesCalcul Tailles = new(
        // Nombres premiers jusqu'à 150 millions.
        Crible: 150_000_000,
        // Produit de deux matrices 1000 × 1000 (un milliard de multiplications).
        Matrice: 1000,
        // Tri de 30 millions d'entiers.
        Tri: 30_000_000,
        // 3 milliards de tours d'un générateur xorshift.
        Hachage: 3_000_000_000);

    // Générateur congruentiel 32 bits (constantes de Numerical Recipes).
    private static uint Suivant(uint graine) => unchecked(graine * 1_664_525u + 1_013_904_223u);

    // Crible d'Ératosthène : nombre de nombres premiers ≤ limite.
    public static int CompterPremiers(int limite)
    {
        var compose = new byte[limite + 1];
        var premiers = 0;
        for (var i = 2; i <= limite; i++)
        {
            if (compose[i] != 0) continue;
            premiers++;
            for (var j = (long)i * i; j <= limite; j += i) compose[j] = 1;
        }
        return premiers;
    }

    // Produit de deux matrices n × n de nombres à virgule ; renvoie la partie
    // entière de la somme de la diagonale du produit.
    public static double ProduitMatrices(int n)
    {
        var a = new double[n * n];
        var b = new double[n * n];
        var c = new double[n * n];
        uint graine = 1;
        for (var i = 0; i < n * n; i++)
        {
            graine = Suivant(graine);
            a[i] = graine / 4_294_967_296d;
            graine = Suivant(graine);
            b[i] = graine / 4_294_967_296d;
        }
        for (var i = 0; i < n; i++)
        {
            for (var k = 0; k < n; k++)
            {
                var aik = a[i * n + k];
                for (var j = 0; j < n; j++) c[i * n + j] += aik * b[k * n + j];
            }
        }
        double trace = 0;
        for (var i = 0; i < n; i++) trace += c[i * n + i];
        return Math.Floor(trace * 1000);
    }

    // Tri d'entiers pseudo-aléatoires ; renvoie la médiane.
    public static uint TrierEntiers(int n)
    {
        var valeurs = new uint[n];
        uint graine = 2;
        for (var i = 0; i < n; i++)
        {
            graine = Suivant(graine);
            valeurs[i] = graine;
        }
        Array.Sort(valeurs);
        return valeurs[n >> 1];
    }

    // Boucle d'arithmétique entière (xorshift 32 bits) ; renvoie l'état final.
    public static uint Hacher(long tours)
    {
        uint x = 2_463_534_242;
        for (long i = 0; i < tours; i++)
        {
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
        }
        return x;
    }

    public static ResultatCalcul Lancer(TaillesCalcul? tailles = null)
    {
        tailles ??= Tailles;
        var debut = DateTime.UtcNow;
        var departTotal = Stopwatch.GetTimestamp();
        var etapes = new List<EtapeCalcul>();
        void Mesurer(string nom, Func<double> calcul)
        {
            var depart = Stopwatch.GetTimestamp();
            var resultat = calcul();
            etapes.Add(new EtapeCalcul(nom, Math.Round(Stopwatch.GetElapsedTime(depart).TotalMilliseconds, 2), resultat));
        }

        Mesurer("Nombres premiers", () => CompterPremiers(tailles.Crible));
        Mesurer("Produit de matrices", () => ProduitMatrices(tailles.Matrice));
        Mesurer("Tri", () => TrierEntiers(tailles.Tri));
        Mesurer("Arithmétique entière", () => Hacher(tailles.Hachage));

        return new ResultatCalcul(debut, DateTime.UtcNow, Math.Round(Stopwatch.GetElapsedTime(departTotal).TotalMilliseconds, 2), etapes);
    }
}
