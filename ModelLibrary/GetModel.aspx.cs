//#define LOAD_REMOTE_MODEL

using System;
using System.Collections.Generic;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

public partial class GetModel : System.Web.UI.Page
{
    private const int bufferSize = 4096;
    private static byte[] buffer = new byte[bufferSize];

    protected void Page_Load(object sender, EventArgs e)
    {
//        Response.Buffer = true;
//        Response.BufferOutput = true;
//        Response.ContentType = "binary";
//        Response.TransmitFile("../3dModels/" + Request["model"] + ".3ds");

        // Original code
        //Response.TransmitFile(Database.GetDataRoot(Server) + "3dModels/" + Request["model"] + ".3ds");
        //Response.End();

        //Response.Redirect("http://voidstar.xtreemhost.com/" + Request["model"], false);
//         CompleteResponse();

        // Fetch the remote 3D model data, and send it to the client.
        // TODO: somehow the binary data is stripped of certain bytes/characters. This was caused by FTP upload in ASCII mode.
        Response.Buffer = true;
        Response.BufferOutput = true;
        Response.ContentType = "binary";

#if LOAD_REMOTE_MODEL

        var inputStream = RenderUtils.FetchRemoteModelAsStream("3dmodels/" + Request["model"]);

#else

        var inputStream = RenderUtils.FetchLocalModelAsStream(Database.GetDataRoot(Server), Request["model"]);

#endif

        using (inputStream)
        {
            int bytesRead;
            while(0 != (bytesRead = inputStream.Read(buffer, 0, bufferSize)))
            {
                Response.OutputStream.Write(buffer, 0, bytesRead);
                //Response.BinaryWrite(stream.Read());
            }

        }
        Response.End();
    }
}