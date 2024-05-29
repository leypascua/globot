using System.Text;

namespace Globot;

public static class EnumerableExtensions
{
    public static string ToCsv(this IEnumerable<string> inputs, string delimiter = ",")
    {
        var sb = new StringBuilder();

        foreach (string input in inputs)
        {
            if (sb.Length > 0)
            {
                sb.Append(delimiter);
            }

            sb.Append(input);
        }

        return sb.ToString();
    }
}
