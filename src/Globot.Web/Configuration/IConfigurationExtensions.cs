namespace Globot.Web.Configuration;

public static class IConfigurationExtensions
{
    public static GlobotConfiguration GlobotConfiguration(this IConfiguration config)
    {
        return config.GetSectionAs<GlobotConfiguration>("Globot");
    }

    public static TConf GetSectionAs<TConf>(this IConfiguration config, string key) where TConf : class, new()
    {
        var result = new TConf();
        var section = config.GetSection(key);

        if (section != null)
        {
            section.Bind(result);
        }

        return result;
    }
}
