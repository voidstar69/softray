#define SHOW_ALL_MODELS

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Web;

public partial class _Default : System.Web.UI.Page
{
    const int thumbnailsPerPage = 35;
    const int thumbnailWidth = 160;
    const int thumbnailHeight = 100;
    const double thumbNailYawDegrees = 220.0;
    const double thumbNailPitchDegrees = -20.0;

    protected void Page_Load(object sender, EventArgs e)
    {
        searchBox.Focus();

        if (!IsPostBack)
        {
            int page;
            int pageSize;
            string searchTerm = Request["query"];
            
            if (!int.TryParse(Request["page"], out page))
            {
                page = 1;
            }

            if (!int.TryParse(Request["pageSize"], out pageSize))
            {
                pageSize = thumbnailsPerPage;
            }

            searchBox.Text = searchTerm;
            FindModels(searchTerm, page, pageSize);
        }
    }

    protected void performSearch(object sender, EventArgs e)
    {
        Redirect("Default.aspx?page=1&query=" + searchBox.Text);
    }

    private void Redirect(string url)
    {
        if (Request.Url.Host == "sculpture3d.com" || Request.Url.Host == "www.sculpture3d.com")
        {
            // For some reason the real website adds in the domain directory. Here we remove it.
            Response.Redirect("../" + url, true);
        }
        else
        {
            Response.Redirect(url, true);
        }
    }

    private void FindModels(string searchTerm, int page, int pageSize)
    {
        if (searchTerm == null)
        {
            return;
        }

        searchTerm = searchTerm.Trim().ToLower();

#if !SHOW_ALL_MODELS
        if (searchTerm == string.Empty)
        {
            return;
        }
#endif

        // Create a list of the names of all the 3D models, filtered according to the search term.

        //var allModelFileNames = GetLocalModelFiles(Database.GetDataRoot(Server) + "3dmodels/", "*.3ds");
        var allModelFileNames = GetRemoteModelFiles(Database.GetDataRoot(Server) + "RemoteModelList.txt");

        var filteredModelFileNames = new List<string>(1000);
        foreach (var modelFileName in allModelFileNames)
        {
            // Extract the name of the model from the 3DS file name.
            string modelName = RemoveFileExtension(modelFileName);

            // Ignore any models whose name do not contain the search term.
            if (string.IsNullOrEmpty(searchTerm) || modelName.ToLower().Contains(searchTerm))
            {
                filteredModelFileNames.Add(modelFileName);
            }
        }

        filteredModelFileNames.Sort();

        int totalPages = (filteredModelFileNames.Count + pageSize - 1) / pageSize;

        // Create an HTML table of thumbnails and hyperlinks to each 3D model.
        StringBuilder builder = new StringBuilder(16 * 1024);
        builder.AppendFormat("<h2>{0} models, page {1} of {2}</h2>", filteredModelFileNames.Count, page, totalPages);

        if (page > 1)
        {
            builder.AppendFormat(@"<a href=""Default.aspx?query={0}&page={1}&pagesize={2}"">Previous Page</a><br>", searchTerm, page - 1, pageSize);
        }

        if (filteredModelFileNames.Count > 0)
        {
            int firstModelIndex = (page - 1) * pageSize;
            firstModelIndex = Clamp(0, firstModelIndex, filteredModelFileNames.Count - 1);
            int lastModelIndex = firstModelIndex + pageSize;
            lastModelIndex = Clamp(0, lastModelIndex, filteredModelFileNames.Count);

            for (int index = firstModelIndex; index < lastModelIndex; index++)
            {
                string modelFileName = filteredModelFileNames[index];

                // Extract the name of the model from the 3DS file name.
                string modelName = RemoveFileExtension(modelFileName);

                // Create a link to view the model.
                builder.AppendFormat(@"<a href=""ModelPreview.aspx?model={0}"">", Server.UrlEncode(modelFileName));

                builder.AppendFormat(@"<image src=""RenderModel.aspx?model={0}&width={1}&height={2}&yaw={3}&pitch={4}"" title=""{5}"" alt=""{5}""/></a>{6}",
                    Server.UrlEncode(modelFileName), thumbnailWidth, thumbnailHeight, thumbNailYawDegrees, thumbNailPitchDegrees, modelName, Environment.NewLine);
            }
        }

        if (page < totalPages)
        {
            builder.AppendFormat(@"<br><a href=""Default.aspx?query={0}&page={1}&pagesize={2}"">Next Page</a><br>", searchTerm, page + 1, pageSize);
        }

        builder.Append("</body></html>");

        searchResults.InnerHtml = builder.ToString();
    }

    private static IList<string> GetLocalModelFiles(string modelDir, string fileExtension)
    {
        var modelFileNames = new List<string>(1000);
        foreach (FileInfo fileInfo in new DirectoryInfo(modelDir).GetFiles(fileExtension))
        {
            // Ignore any file names that start with an underscore - webserver related _vti_cnf files appear sometimes.
            if (fileInfo.Name.StartsWith("_"))
            {
                continue;
            }

            modelFileNames.Add(fileInfo.Name);
        }
        return modelFileNames;
    }

    private static IList<string> GetRemoteModelFiles(string modelListFilePath)
    {
        return File.ReadAllLines(modelListFilePath);
    }

    private static string RemoveFileExtension(string filePath)
    {
        int index = filePath.LastIndexOf('.');
        if (index == -1)
        {
            return filePath;
        }
        else
        {
            return filePath.Substring(0, index);
        }
    }

    private int Clamp(int min, int val, int max)
    {
        Debug.Assert(min <= max);
        return Math.Min(Math.Max(min, val), max);
    }
}
