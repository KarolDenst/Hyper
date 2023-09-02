﻿using Hyper.MathUtils;
using Hyper.Meshes;
using OpenTK.Graphics.OpenGL4;

namespace Hyper.Shaders;
internal static class ShaderFactory
{
    public static Shader CreateObjectShader()
    {
        var shaderParams = new[]
        {
            ("Shaders/lighting_shader.vert", ShaderType.VertexShader),
            ("Shaders/lighting_shader.frag", ShaderType.FragmentShader)
        };

        return new Shader(shaderParams);
    }

    public static Shader CreateLightSourceShader()
    {
        var shaderParams = new[]
        {
            ("Shaders/lighting_shader.vert", ShaderType.VertexShader),
            ("Shaders/light_source_shader.frag", ShaderType.FragmentShader)
        };
        return new Shader(shaderParams);
    }

    public static Shader CreateModelShader()
    {
        var shader = new[]
        {
            ("Animation/Shaders/model_shader.vert", ShaderType.VertexShader),
            ("Animation/Shaders/model_shader.frag", ShaderType.FragmentShader)
        };
        return new Shader(shader);
    }

    public static void SetUpObjectShaderParams(Shader objectShader, Camera camera, List<LightSource> lightSources, float globalScale, int sphere)
    {
        objectShader.Use();
        objectShader.SetFloat("curv", WorldProperties.Instance.Curve);
        objectShader.SetFloat("anti", 1.0f);
        //if (sphere == 0)
        objectShader.SetMatrix4("view", camera.GetViewMatrix());
        //else
        //    objectShader.SetMatrix4("view", camera.GetViewMatrix2());
        objectShader.SetMatrix4("projection", camera.GetProjectionMatrix());
        objectShader.SetInt("numLights", lightSources.Count);
        objectShader.SetVector4("viewPos", GeomPorting.EucToCurved(camera.ViewPosition, WorldProperties.Instance.Curve));

        objectShader.SetVector3Array("lightColor", lightSources.Select(x => x.Color).ToArray());
        objectShader.SetVector4Array("lightPos", lightSources.Select(x =>
            GeomPorting.EucToCurved(GeomPorting.CreateTranslationTarget(x.Position, camera.ReferencePointPosition, WorldProperties.Instance.Curve) * globalScale, WorldProperties.Instance.Curve)).ToArray());
    }

    public static void SetUpLightingShaderParams(Shader lightSourceShader, Camera camera)
    {
        lightSourceShader.Use();
        lightSourceShader.SetFloat("curv", WorldProperties.Instance.Curve);
        lightSourceShader.SetFloat("anti", 1.0f);
        lightSourceShader.SetMatrix4("view", camera.GetViewMatrix());
        lightSourceShader.SetMatrix4("projection", camera.GetProjectionMatrix());
    }

    public static void SetUpCharacterShaderParams(Shader characterShader, Camera camera, List<LightSource> lightSources, float globalScale, int sphere = 0)
    {
        characterShader.Use();
        characterShader.SetFloat("curv", WorldProperties.Instance.Curve);
        characterShader.SetMatrix4("view", camera.GetViewMatrix());
        characterShader.SetMatrix4("projection", camera.GetProjectionMatrix());

        characterShader.SetInt("numLights", lightSources.Count);
        characterShader.SetVector4("viewPos", GeomPorting.EucToCurved(camera.ViewPosition, WorldProperties.Instance.Curve));
        characterShader.SetVector3Array("lightColor", lightSources.Select(x => x.Color).ToArray());
        characterShader.SetVector4Array("lightPos", lightSources.Select(x =>
            GeomPorting.EucToCurved(GeomPorting.CreateTranslationTarget(x.Position, camera.ReferencePointPosition, WorldProperties.Instance.Curve) * globalScale, WorldProperties.Instance.Curve)).ToArray());
    }
}
