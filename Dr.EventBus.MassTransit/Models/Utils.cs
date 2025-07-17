using System.Text;

namespace Dr.EventBus.MassTransit.Models;

public static class Utils
{
    public static string GenerateRepairEventName(this string eventName)
    {
        eventName = eventName.KebabCase();
        return eventName.ToLower() + "-repair";
    }

    public static string KebabCase(this string str)
    {
        if (string.IsNullOrWhiteSpace(str))
            return string.Empty;

        var sb = new StringBuilder();
        for (int i = 0; i < str.Length; i++)
        {
            char c = str[i];

            if (char.IsUpper(c))
            {
                if (i > 0 && (char.IsLower(str[i - 1]) || char.IsDigit(str[i - 1])))
                    sb.Append('-');

                sb.Append(char.ToLower(c));
            }
            else if (c == '_' || c == ' ')
            {
                sb.Append('-');
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

}