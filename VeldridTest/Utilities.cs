using System;
using System.Timers;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace SDFbox
{
    class Utilities
    {
        public static Sdl2Window MakeWindow(int width, int height)
        {
            WindowCreateInfo windowCI = new WindowCreateInfo() {
                X = 100,
                Y = 100,
                WindowWidth = width,
                WindowHeight = height,
                WindowTitle = "SDFbox",
            };
            Sdl2Window window = VeldridStartup.CreateWindow(ref windowCI);
            window.CursorVisible = false;

            return window;
        }

        public static void SetStencilState(GraphicsPipelineDescription pipelineDescription,
            bool depthTest = true, bool depthWrite = true,
            ComparisonKind comparison = ComparisonKind.LessEqual)
        {
            pipelineDescription.DepthStencilState = new DepthStencilStateDescription(
                depthTestEnabled: depthTest,
                depthWriteEnabled: depthWrite,
                comparisonKind: comparison);
        }

        public static void SetRasterizerState(GraphicsPipelineDescription pipelineDescription,
            FaceCullMode faceCull = FaceCullMode.Back,
            PolygonFillMode polygonFill = PolygonFillMode.Solid,
            FrontFace front = FrontFace.Clockwise,
            bool depthClip = true, bool scissorTest = false)
        {
            pipelineDescription.RasterizerState = new RasterizerStateDescription(
                cullMode: faceCull,
                fillMode: polygonFill,
                frontFace: front,
                depthClipEnabled: depthClip,
                scissorTestEnabled: scissorTest);
        }


        public static ShaderSetDescription MakeShaderSet(Shader vertexShader, Shader fragmentShader)
        {
            return (new ShaderSetDescription(
                new VertexLayoutDescription[] { LayoutDescription() },
                new Shader[] { vertexShader, fragmentShader }));
        }

        static VertexLayoutDescription LayoutDescription()
        {
            return (new VertexLayoutDescription(
            new VertexElementDescription("Position", VertexElementSemantic.Position, VertexElementFormat.Float2),
            new VertexElementDescription("Color", VertexElementSemantic.Color, VertexElementFormat.Float4)));
        }


    }

    class FPS
    {
        Timer secondTimer;
        int frame = 0;
        public FPS()
        {
            secondTimer = new Timer(1000);
            secondTimer.Elapsed += sec;
            secondTimer.AutoReset = true;
            secondTimer.Enabled = true;
        }
        ~FPS()
        {
            secondTimer.Stop();
            secondTimer.Dispose();
        }
        public void Frame()
        {
            frame++;
        }

        void sec(Object source, ElapsedEventArgs e)
        {
            Console.Clear();
            try {
                Console.WriteLine(1000 / frame + " mspf - " + frame + "fps");
            } catch (System.DivideByZeroException ex) {
                Console.WriteLine("No frames in this second");
            }
            frame = 0;
        }
    }
}
