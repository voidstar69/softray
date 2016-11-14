using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Engine3D
{
    public class Scene
    {
        public Vector cameraPos;                    // position of camera (in model space)

        // Lighting parameters (in object space)
        // TODO: extend this to support multiple lights of different types
        // TODO: add a range/falloff for position light, especially to control area lighting for soft shadows
        public double ambientLight_intensity;
        public Vector directionalLightDir_Model;    // direction of light (in model space)
        public Vector directionalLightDir_View;     // direction of light (in view space)
        public Vector positionalLightPos_Model;     // position of light (in model space)
        public Vector positionalLightPos_View;      // position of light (in view space)
        public double specularLight_shininess;
        public bool pointLighting = true;           // Shade using a point light? Otherwise a directional light is used.
        public bool specularLighting = true;        // Include specular lighting? Diffuse and ambient lighting are always used.
    }
}