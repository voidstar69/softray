using System.IO;
using System.Net;
using Engine3D;

// Handles fetching and caching of 3D models and rendered images.
public static class RenderUtils
{
    public static string RemoteHost = "http://voidstar.xtreemhost.com/";

    // TODO: Not cached for now
    public static Stream FetchLocalModelAsStream(string dataRoot, string modelFileName)
    {
        string modelFilePath = string.Format("{0}3dModels\\{1}", dataRoot, modelFileName);
        return new FileStream(modelFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    // TODO: Not cached for now
    public static Stream FetchRemoteModelAsStream(string modelFilePath)
    {
        string address = RemoteHost + modelFilePath;
        using (var webClient = new WebClient())
        {
            // Add a user agent header so that the remote web server does not discriminate against us.
            webClient.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");
            return webClient.OpenRead(address);
        }
    }

    // Cached in memory.
    //public static Model FetchModelAsObject(string modelFileName)
    //{
    //}
}