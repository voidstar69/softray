using System;

public partial class ModelPreview : System.Web.UI.Page
{
    const int defaultImageWidth = 1024;
    const int defaultImageHeight = 768;
    const double pitchDeltaInDegrees = 20.0;
    const double yawDeltaInDegrees = 20.0;
    const double initialPitchInDegrees = -pitchDeltaInDegrees;
    const double initialYawInDegrees = yawDeltaInDegrees * 11.0;

    bool rayTrace = false;
    bool rayTraceLightField = false;

    // The values in these variables is lost between page refreshes (i.e. when the image changes),
    // but are repopulated by GetAngles.
    double pitchInDegrees;
    double yawInDegrees;

    protected void Page_Load(object sender, EventArgs e)
    {
        view3DButton.PostBackUrl = "Viewer3D.aspx?model=" + Request["model"];
        view3DHWButton.PostBackUrl = "ViewerXNA3D.aspx?model=" + Request["model"];

        GetParam("raytrace", ref rayTrace);
        GetParam("lightfield", ref rayTraceLightField);
        if (rayTraceLightField)
            rayTrace = true;

        if (!IsPostBack)
        {
            // Original landing on page.
            pitchInDegrees = initialPitchInDegrees;
            yawInDegrees = initialYawInDegrees;
            setAngles();

            // Is animate parameter missing?
            if (Request["animate"] == null)
            {
                // Yes, so default to animating.
                updateAnimation();
            }

            // Currently not animating?
            if (Request["animate"] == "0")
            {
                // Yes, so show still image.
                AnimateButton.Text = "Animate";
                updateStillImage();
            }
            else
            {
                // No.
                AnimateButton.Text = "Freeze";
            }
        }

//        label.Text = string.Format("ASP.NET: Pitch: {0}    Yaw: {1}", pitchInDegrees, yawInDegrees);
    }

    protected void rotateLeftButton_Click(object sender, EventArgs e)
    {
        getAngles();
        yawInDegrees -= yawDeltaInDegrees;
        update();
    }

    protected void rotateRightButton_Click(object sender, EventArgs e)
    {
        getAngles();
        yawInDegrees += yawDeltaInDegrees;
        update();
    }

    protected void rotateUpButton_Click(object sender, EventArgs e)
    {
        getAngles();
        pitchInDegrees -= pitchDeltaInDegrees;
        update();
    }

    protected void rotateDownButton_Click(object sender, EventArgs e)
    {
        getAngles();
        pitchInDegrees += pitchDeltaInDegrees;
        update();
    }

    protected void lightfieldButton_Click(object sender, EventArgs e)
    {
        getAngles();
        rayTraceLightField = !rayTraceLightField;
        rayTrace = rayTraceLightField;
        update();
    }

    protected void raytraceButton_Click(object sender, EventArgs e)
    {
        getAngles();
        rayTrace = !rayTrace;
        rayTraceLightField = false;
        update();
    }

    private void ChangeParams(string parameters)
    {
        if (Request.Url.Host == "sculpture3d.com" || Request.Url.Host == "www.sculpture3d.com")
        {
            // For some reason the real website adds in the domain directory. Here we remove it.
            Response.Redirect("../ModelPreview.aspx?" + parameters, true);
        }
        else
        {
            Response.Redirect("ModelPreview.aspx?" + parameters, true);
        }
    }

    protected void AnimateButton_Click(object sender, EventArgs e)
    {
        getAngles();

        // Currently animating model?
        if (Request["animate"] == "1")
        {
            // Yes, so now show still image.
            ChangeParams("model=" + Request["model"] + "&animate=0");
        }
        else
        {
            // No, so now show animation.
            updateAnimation();
        }
/*
        bool animate = GetGlobalBoolean("animate");
        animate = !animate;
        SetGlobal("animate", animate);
        if (animate)
        {
            AnimateNextFrame();
        }
*/
    }

    private void getAngles()
    {
        pitchInDegrees = GetGlobalDouble("pitch");
        yawInDegrees = GetGlobalDouble("yaw");
    }

    private void setAngles()
    {
        // Clamp pitch to lie within [-90, +90] degrees.
        pitchInDegrees = Math.Min(Math.Max(-90.0, pitchInDegrees), 90.0);

        // Wrap yaw to lie within [0, 360) degrees.
        yawInDegrees = yawInDegrees % 360.0;
        if (yawInDegrees < 0)
        {
            yawInDegrees += 360.0;
        }

        SetGlobal("pitch", pitchInDegrees);
        SetGlobal("yaw", yawInDegrees);
    }

    private void update()
    {
        setAngles();

        // Currently animating model?
        if (Request["animate"] == "1")
        {
            // Yes, so now show animation.
            updateAnimation();
        }
        else
        {
            // No, so now show still image.
            updateStillImage();
        }

        //        label.Text = string.Format("ASP.NET: Pitch: {0}    Yaw: {1}", pitchInDegrees, yawInDegrees);
    }

    private void updateAnimation()
    {
        var imageWidth = defaultImageWidth;
        var imageHeight = defaultImageHeight;
        GetParam("width", ref imageWidth);
        GetParam("height", ref imageHeight);

        ChangeParams("model=" + Request["model"] + "&width=" + imageWidth + "&height=" + imageHeight + "&animate=1&yaw=" + yawInDegrees + "&pitch=" + pitchInDegrees + "&smoothness=1" + "&raytrace=" + rayTrace.ToString() + "&lightfield=" + rayTraceLightField.ToString() + "&depth=" + Request["depth"]);
    }

    private void updateStillImage()
    {
        // Get the name of the model file.
        string modelFileName = Request["model"];

        // TODO
//        Request.Browser.ScreenPixelsWidth

        var imageWidth = defaultImageWidth;
        var imageHeight = defaultImageHeight;
        GetParam("width", ref imageWidth);
        GetParam("height", ref imageHeight);

        // Point the image on this page to this new image file.
        // TODO: pass all request parameters onto the RenderModel page
        modelImage.ImageUrl = string.Format("RenderModel.aspx?model={0}&width={1}&height={2}&yaw={3}&pitch={4}&raytrace={5}&lightfield={6}",
            Server.UrlEncode(modelFileName), imageWidth, imageHeight, yawInDegrees, pitchInDegrees, rayTrace.ToString(), rayTraceLightField.ToString());
//        formBody.InnerHtml = string.Format(@"<image src=""RenderModel.aspx?model={0}&width={1}&height={2}&yaw={3}&pitch={4}""",
//            Server.UrlEncode(modelFileName), imageWidth, imageHeight, yawInDegrees, initialPitchInDegrees);
    }

    // TODO: is there a better way to persist this than with cookies?
    private void SetGlobal(string name, object value)
    {
        Session[name] = value;
//        Response.Cookies[name].Value = value.ToString();
    }

    private object GetGlobal(string name)
    {
        return Session[name];
/*
        if (Request.Cookies[name] != null)
        {
            return Request.Cookies[name].Value;
        }
        else
        {
            return null;
        }
*/
    }

    private double GetGlobalDouble(string name)
    {
        object obj = GetGlobal(name);
        if (obj == null)
        {
            return 0.0;
        }
        else
        {
            return (double)GetGlobal(name);
        }
/*
        double num = 0.0;
        double.TryParse(GetGlobal(name), out num);
        return num;
*/
    }

    private bool GetGlobalBoolean(string name)
    {
        object obj = GetGlobal(name);
        if (obj == null)
        {
            return false;
        }
        else
        {
            return (bool)GetGlobal(name);
        }
/*
        bool flag = false;
        bool.TryParse(GetGlobal(name), out flag);
        return flag;
*/
    }

    private void GetParam(string paramName, ref string paramVar)
    {
        if (Request[paramName] != null)
        {
            paramVar = Request[paramName];
        }
    }

    private void GetParam(string paramName, ref int paramVar)
    {
        if (Request[paramName] != null)
        {
            int.TryParse(Request[paramName], out paramVar);
        }
    }

    private void GetParam(string paramName, ref double paramVar)
    {
        if (Request[paramName] != null)
        {
            double.TryParse(Request[paramName], out paramVar);
        }
    }

    private void GetParam(string paramName, ref bool paramVar)
    {
        if (Request[paramName] != null)
        {
            bool.TryParse(Request[paramName], out paramVar);
        }
    }
}