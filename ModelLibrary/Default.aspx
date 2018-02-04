<%@ Page Language="C#" AutoEventWireup="true" CodeFile="Default.aspx.cs" Inherits="_Default" %>
<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
  <title>Free 3D Virtual Model Gallery</title>
</head>
<body>
  <div align="center">
<!--
    <a href="?query=sculpture"><img title="sculpture" src="RenderModel.aspx?model=sculpture-head.3ds&width=160&height=100&yaw=220&pitch=-20"/></a>
    <a href="?query=car"><img title="car" src="RenderModel.aspx?model=gablota-sportscar.3DS&width=160&height=100&yaw=220&pitch=-20"/></a>
    <a href="?query=house"><img title="house" src="RenderModel.aspx?model=detached_house1.3DS&width=160&height=100&yaw=220&pitch=-20"/></a>
    <a href="?query=parfume"><img title="parfume" src="RenderModel.aspx?model=03parfume-solution.3DS&width=160&height=100&yaw=220&pitch=-20"/></a>
    <a href="?query=chess"><img title="chess" src="RenderModel.aspx?model=cHESS.3ds&width=160&height=100&yaw=220&pitch=-20"/></a>
    <a href="?query=cathedral"><img title="cathedral" src="RenderModel.aspx?model=75Cathedral-model.3DS&width=160&height=100&yaw=220&pitch=-20"/></a>
    <a href="?query=boat"><img title="boat" src="RenderModel.aspx?model=64sailing-boat.3DS&width=160&height=100&yaw=220&pitch=-20"/></a>
    <a href="?query=dino"><img title="dino" src="RenderModel.aspx?model=dino--55k.3ds&width=160&height=100&yaw=220&pitch=-20"/></a>
    <a href="?query=tank"><img title="tank" src="RenderModel.aspx?model=04military-tank-3d.3DS&width=160&height=100&yaw=220&pitch=-20"/></a>
    <a href="?query=ferrari"><img title="ferrari" src="RenderModel.aspx?model=82ferrari_spider.3DS&width=160&height=100&yaw=220&pitch=-20"/></a>
    <a href="?query=flower"><img title="flower" src="RenderModel.aspx?model=flower1.3ds&width=160&height=100&yaw=220&pitch=-20"/></a>
    <a href="?query=chair"><img title="chair" src="RenderModel.aspx?model=oldchairs.3DS&width=160&height=100&yaw=220&pitch=-20"/></a>
-->

    <h1>Free 3D Virtual Model Gallery</h1>
    Welcome to this gallery of 3D models created by computer artists.
    Please type in a word that you would like to search for.
    <br />
    <br />
    Click on the above images for search examples.
    <br />
    <br />
    <br />
    <form id="modelForm" runat="server">
      <div>
        <asp:TextBox ID="searchBox" runat="server" AutoPostBack="true" 
          AutoCompleteType="Search" EnableViewState="True" 
          ontextchanged="performSearch" TabIndex="2" />
        &nbsp;
        <asp:Button ID="searchButton" runat="server" Text="Search" 
          onclick="performSearch" TabIndex="1" />
      </div>
    </form>
    <div id="searchResults" runat="server" />
    <br />
    <br />

    <!-- Link to the fly-through gallery -->
    <a href="ViewerXNA3D.aspx?gallery=true&gallerySize=10">Fly-through Gallery</a>

  </div>
  
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