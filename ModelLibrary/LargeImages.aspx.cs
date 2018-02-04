using System;
using System.Collections.Generic;
using System.IO;
using System.Web;

public partial class _Default : System.Web.UI.Page
{
    // TODO: for caching the large views on the webserver
    const int thumbnailsPerRow = 1;
    const int thumbnailWidth = 1024;
    const int thumbnailHeight = 768;
    const double thumbNailYawDegrees = 220.0;
    const double thumbNailPitchDegrees = -20.0;
    const bool rayTrace = false;

    protected void Page_Load(object sender, EventArgs e)
    {
        Response.Write("<html xmlns=\"http://www.w3.org/1999/xhtml\">");
        Response.Write("<head>");
        Response.Write("<title>3D Model Library</title>");
        Response.Write("</head>");
        Response.Write("<body>");
        Response.Write("<h1>3D Model Library</h1>");

        // Get file information for all the 3D models.
        string dataModelDir = Database.GetDataRoot(Server) + "3dmodels/";
        FileInfo[] allFilesInfo = new DirectoryInfo(dataModelDir).GetFiles("*.3ds");

        Response.Write(string.Format("<h2>{0} models</h2>", allFilesInfo.Length));

        // Create a table of thumbnails and hyperlinks to each 3D model.
        Response.Write(@"<table border=""1"">");
        Response.Write("<tr>");
        int thumbnailIndex = 0;
        foreach (FileInfo fileInfo in allFilesInfo)
        {
            // Ignore any file names that start with an underscore - webserver related _vti_cnf files appear sometimes.
            if(fileInfo.Name.StartsWith("_"))
            {
                continue;
            }

            // Extract the name of the model from the 3DS file name.
            string modelName = fileInfo.Name.Substring(0, fileInfo.Name.LastIndexOf('.'));

            // Time to create a new row in the table?
            if (thumbnailIndex % thumbnailsPerRow == 0)
            {
                Response.Write("</tr>");
                Response.Write("<tr>");
            }

            Response.Write("<td>");

            // Create a link to view the model.
            Response.Write(string.Format(@"<a href=""Viewer3D.aspx?model={0}"">", Server.UrlEncode(modelName)));

            Response.Write(string.Format(@"<image src=""RenderModel.aspx?model={0}&width={1}&height={2}&yaw={3}&pitch={4}&rayTrace={5}""/></a>",
                Server.UrlEncode(modelName), thumbnailWidth, thumbnailHeight, thumbNailYawDegrees, thumbNailPitchDegrees, rayTrace.ToString()));

            Response.Write(string.Format(@"<br><a href=""ModelPreview.aspx?model={0}""/>{1}</a>",
                Server.UrlEncode(modelName), modelName));

            Response.Write("</td>");

            thumbnailIndex++;
        }
        Response.Write("</tr>");
        Response.Write("</table>");

        Response.Write("</body>");
        Response.Write("</html>");
        Response.End();
    }
}
