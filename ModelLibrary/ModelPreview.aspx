<%@ Page Language="C#" AutoEventWireup="true" CodeFile="ModelPreview.aspx.cs" Inherits="ModelPreview" Debug="true" %>
<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
  <title>Model Preview</title>
</head>
<body>
  <form id="modelPreviewForm" runat="server">
    <a href="Default.aspx">Search</a>&nbsp;&nbsp;&nbsp;
    <a href="javascript:history.back()">Back</a>
    &nbsp;&nbsp;&nbsp;
    <asp:LinkButton ID="view3DButton" runat="server">View in 3D</asp:LinkButton>
    <asp:LinkButton ID="view3DHWButton" runat="server">View with 3D HW</asp:LinkButton>
    &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;
    <asp:Button ID="AnimateButton" runat="server" onclick="AnimateButton_Click" 
      Text="Animate" />
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;
    <asp:Button ID="rotateUpButton" runat="server" Text="Move Up" onclick="rotateUpButton_Click" />
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;
    <asp:Button ID="raytraceButton" runat="server" Text="Raytrace" onclick="raytraceButton_Click" />
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;
    <asp:Button ID="lightfieldButton" runat="server" Text="Light Field" onclick="lightfieldButton_Click" />
    <br />
<!--    <div id="formBody" runat="server" /> -->
<!--    <img id="image" src="" onload="onImageLoaded()" />  -->
    <asp:Button ID="rotateLeftButton" runat="server" Text="Move Left" onclick="rotateLeftButton_Click" />
    <asp:Image ID="modelImage" runat="server" />
    <asp:Button ID="rotateRightButton" runat="server" Text="Move Right" onclick="rotateRightButton_Click" />
    <br />
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;
    <asp:Button ID="rotateDownButton" runat="server" Text="Move Down" onclick="rotateDownButton_Click" />

  </form>
  <script type="text/javascript">
    var modelName;
    var width = 1024;
    var height = 768;
    var yawDegrees = 0.0;
    var pitchDegrees = 0.0;
    var yawDeltaDegrees = 10.0;
    var imageLoadDelay = 100; // milliseconds
    var rayTrace = false;
    var rayTraceLightField = false;
    var timeoutId;
  
    init();

    function init() {
      animate = getUrlParameter("animate");
      if (animate == "1") {
        modelName = getUrlParameter("model");
        width = getUrlParameter("width");
        height = getUrlParameter("height");
        yawDegrees = parseFloat(getUrlParameter("yaw"));
        pitchDegrees = parseFloat(getUrlParameter("pitch"));
        rayTrace = (getUrlParameter("raytrace") == "true" || getUrlParameter("raytrace") == "True");
        rayTraceLightField = (getUrlParameter("lightfield") == "true" || getUrlParameter("lightfield") == "True");
        smoothness = parseInt(getUrlParameter("smoothness"));
        if (smoothness > 0)
        {
          yawDeltaDegrees = 10.0 / smoothness;
        }
        
        img = document.getElementById("modelImage");
        img.onload = onImageLoaded;
        
        onImageLoaded();
      }
    }

    function onImageLoaded() {
      clearTimeout(timeoutId);
      timeoutID = setTimeout("loadNextImage()", imageLoadDelay);
    }

    function loadNextImage() {
//      jslabel.innerText = "JS: pitch = " + pitchDegrees + "      yaw = " + yawDegrees;
    
      img = document.getElementById("modelImage");
      // TODO: pass all request parameters onto the RenderModel page
      img.src = "RenderModel.aspx?model=" + modelName + "&width=" + width +
                "&height=" + height + "&yaw=" + yawDegrees + "&pitch=" + pitchDegrees + "&rayTrace=" + rayTrace + "&lightfield=" + rayTraceLightField;

      // update and wrap angles
      yawDegrees += yawDeltaDegrees;
      if (yawDegrees < 0) {
          yawDegrees += 360.0;
      }
      yawDegrees = yawDegrees % 360.0;
    }

    function getUrlParameter(name) {
      name = name.replace(/[\[]/, "\\\[").replace(/[\]]/, "\\\]");
      var regexS = "[\\?&]" + name + "=([^&#]*)";
      var regex = new RegExp(regexS);
      var results = regex.exec(window.location.href);
      if (results == null)
        return "";
      else
        return results[1];
    }
  </script>
  
  <!-- Google Analytics -->
<!--
  <script type="text/javascript">
  var gaJsHost = (("https:" == document.location.protocol) ? "https://ssl." : "http://www.");
  document.write(unescape("%3Cscript src='" + gaJsHost + "google-analytics.com/ga.js' type='text/javascript'%3E%3C/script%3E"));
  </script>
  <script type="text/javascript">
  try {
  var pageTracker = _gat._getTracker("UA-3447715-2");
  pageTracker._trackPageview();
  } catch(err) {}</script>
-->
  
</body>
</html>
