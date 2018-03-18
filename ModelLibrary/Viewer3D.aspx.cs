using System;
using System.Web;

public partial class Viewer3D : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        Response.Buffer = true;
        Response.BufferOutput = true;
        Response.WriteFile("Viewer3D.html");
//        Response.TransmitFile("Viewer3D.html");

        // Complete Silverlight HTML tag
        Response.Write("<param name=\"InitParams\" value=\"modelName=" + Request["model"] + ",debug=" + Request["debug"] + "\" />");
        Response.Write(Environment.NewLine);
        Response.Write(@"</object><iframe id=""_sl_historyFrame"" style=""visibility:hidden;height:0px;width:0px;border:0px""></iframe></div>");
        Response.Write(Environment.NewLine);

        // Google Analytics
        // TODO: Make this asynchronous so that it doesn't slow down the page load.
        //Response.Write(@"<script type=""text/javascript""> var gaJsHost = ((""https:"" == document.location.protocol) ? ""https://ssl."" : ""http://www.""); document.write(unescape(""%3Cscript src='""+gaJsHost+""google-analytics.com/ga.js' type='text/javascript'%3E%3C/script%3E""));</script><script type=""text/javascript""> try{var pageTracker = _gat._getTracker(""UA-3447715-2"");pageTracker._trackPageview();}catch(err){}</script>");
        //Response.Write(Environment.NewLine);

        Response.Write("</form>");
        Response.Write(Environment.NewLine);
        Response.Write("</body></html>");
        Response.Write(Environment.NewLine);
        Response.End();
    }
}
