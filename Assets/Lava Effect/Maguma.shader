// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Maguma"
{
	Properties
	{
		_FlowMap("FlowMap", 2D) = "white" {}
		_Speed("Speed", Float) = 1
		_Diffuse(" ", 2D) = "white" {}
		_Emissive("Emissive", 2D) = "white" {}
		_Normal("Normal", 2D) = "white" {}
		_Tiling("Tiling", Float) = 1
		_Metalic("Metalic", Range( 0 , 1)) = 0.5
		_Smoothness("Smoothness", Range( 0 , 1)) = 0.5
		_Strength("Strength", Float) = 0
		[HDR]_EmissiveColor("Emissive Color", Color) = (0,0,0,0)
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Opaque"  "Queue" = "Geometry+0" "IsEmissive" = "true"  }
		Cull Back
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#pragma target 3.0
		#pragma surface surf Standard keepalpha addshadow fullforwardshadows 
		struct Input
		{
			float2 uv_texcoord;
		};

		uniform sampler2D _Normal;
		uniform sampler2D _FlowMap;
		uniform float4 _FlowMap_ST;
		uniform float _Speed;
		uniform float _Strength;
		uniform float _Tiling;
		uniform sampler2D _Diffuse;
		uniform sampler2D _Emissive;
		uniform float4 _EmissiveColor;
		uniform float _Metalic;
		uniform float _Smoothness;

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 uv_FlowMap = i.uv_texcoord * _FlowMap_ST.xy + _FlowMap_ST.zw;
			float2 blendOpSrc17 = i.uv_texcoord;
			float2 blendOpDest17 = (tex2D( _FlowMap, uv_FlowMap )).rg;
			float2 temp_output_17_0 = ( saturate( (( blendOpDest17 > 0.5 ) ? ( 1.0 - 2.0 * ( 1.0 - blendOpDest17 ) * ( 1.0 - blendOpSrc17 ) ) : ( 2.0 * blendOpDest17 * blendOpSrc17 ) ) ));
			float temp_output_10_0 = ( _Time.y * _Speed );
			float temp_output_1_0_g1 = temp_output_10_0;
			float temp_output_14_0 = (0.0 + (( ( temp_output_1_0_g1 - floor( ( temp_output_1_0_g1 + 0.5 ) ) ) * 2 ) - -1.0) * (1.0 - 0.0) / (1.0 - -1.0));
			float timeA19 = ( -temp_output_14_0 * _Strength );
			float2 lerpResult18 = lerp( i.uv_texcoord , temp_output_17_0 , timeA19);
			float2 temp_cast_0 = (_Tiling).xx;
			float2 uv_TexCoord29 = i.uv_texcoord * temp_cast_0;
			float2 DiffuseTiling31 = uv_TexCoord29;
			float2 FlowA21 = ( lerpResult18 + DiffuseTiling31 );
			float temp_output_1_0_g2 = (temp_output_10_0*0.5 + 0.0);
			float timeB43 = ( -(0.0 + (( ( temp_output_1_0_g2 - floor( ( temp_output_1_0_g2 + 0.5 ) ) ) * 2 ) - -1.0) * (1.0 - 0.0) / (1.0 - -1.0)) * _Strength );
			float2 lerpResult44 = lerp( i.uv_texcoord , temp_output_17_0 , timeB43);
			float2 FlowB47 = ( lerpResult44 + DiffuseTiling31 );
			float BlendTime57 = saturate( abs( ( 1.0 - ( temp_output_14_0 / 0.5 ) ) ) );
			float3 lerpResult67 = lerp( UnpackNormal( tex2D( _Normal, FlowA21 ) ) , UnpackNormal( tex2D( _Normal, FlowB47 ) ) , BlendTime57);
			float3 Normal66 = lerpResult67;
			o.Normal = Normal66;
			float4 lerpResult50 = lerp( tex2D( _Diffuse, FlowA21 ) , tex2D( _Diffuse, FlowB47 ) , BlendTime57);
			float4 DiffuseA25 = lerpResult50;
			o.Albedo = DiffuseA25.rgb;
			float4 lerpResult82 = lerp( ( tex2D( _Emissive, FlowA21 ) * _EmissiveColor ) , ( tex2D( _Emissive, FlowB47 ) * _EmissiveColor ) , BlendTime57);
			float4 Emissive83 = lerpResult82;
			o.Emission = Emissive83.rgb;
			o.Metallic = _Metalic;
			o.Smoothness = _Smoothness;
			o.Alpha = 1;
		}

		ENDCG
	}
	Fallback "Diffuse"
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=18935
74.4;166.4;1311;549;1509.951;-467.6626;2.519855;True;True
Node;AmplifyShaderEditor.CommentaryNode;34;-3342.913,140.25;Inherit;False;2279.146;1079.483;Comment;19;19;43;42;15;41;14;12;40;39;10;11;9;53;55;56;57;71;72;73;Time_;1,1,1,1;0;0
Node;AmplifyShaderEditor.SimpleTimeNode;9;-3270.482,190.25;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;11;-3292.913,420.2969;Inherit;False;Property;_Speed;Speed;1;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;10;-3072.482,223.25;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode;39;-2882.182,594.8589;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0.5;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;12;-2843.304,250.9413;Inherit;False;Sawtooth Wave;-1;;1;289adb816c3ac6d489f255fc3caf5016;0;1;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;40;-2620.957,615.4839;Inherit;False;Sawtooth Wave;-1;;2;289adb816c3ac6d489f255fc3caf5016;0;1;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TFHCRemapNode;41;-2401.721,696.0353;Inherit;False;5;0;FLOAT;0;False;1;FLOAT;-1;False;2;FLOAT;1;False;3;FLOAT;0;False;4;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.TFHCRemapNode;14;-2624.068,331.4926;Inherit;False;5;0;FLOAT;0;False;1;FLOAT;-1;False;2;FLOAT;1;False;3;FLOAT;0;False;4;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;36;-2690.823,-2059.075;Inherit;False;844.2578;409.6425;Comment;3;29;30;31;Diffuse_Tiling;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;33;-3892.901,-1411.346;Inherit;False;2237.578;1085.46;Comment;13;21;37;18;38;17;20;2;16;1;44;45;46;47;FlowMap_main;1,1,1,1;0;0
Node;AmplifyShaderEditor.NegateNode;42;-2043.227,669.2427;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.NegateNode;15;-2188.783,324.6024;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;73;-2035.705,574.7054;Inherit;False;Property;_Strength;Strength;8;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;30;-2640.823,-2009.075;Inherit;False;Property;_Tiling;Tiling;5;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;71;-1862.083,382.0593;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;72;-1838.327,663.9055;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;1;-3734.317,-777.4523;Inherit;True;Property;_FlowMap;FlowMap;0;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ComponentMaskNode;2;-3389.334,-842.0482;Inherit;False;True;True;False;False;1;0;COLOR;0,0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;29;-2309.8,-1949.633;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TextureCoordinatesNode;16;-3375.31,-1208.684;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode;19;-1581.771,360.2625;Inherit;False;timeA;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;43;-1499.619,685.3;Inherit;False;timeB;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;20;-2628.374,-1015.486;Inherit;False;19;timeA;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;45;-2739.456,-543.4396;Inherit;False;43;timeB;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.BlendOpsNode;17;-3005.735,-824.4154;Inherit;False;Overlay;True;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;31;-2049.926,-1925.33;Inherit;False;DiffuseTiling;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;18;-2385.157,-1241.773;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;44;-2425.855,-657.9709;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;53;-2316.772,983.1028;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0.5;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;38;-2377.667,-905.9127;Inherit;False;31;DiffuseTiling;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;46;-2116.929,-700.1616;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;37;-2119.939,-1048.546;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.OneMinusNode;54;-2083.332,1016.89;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.AbsOpNode;55;-1843.748,989.2462;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;21;-1864.532,-1211.845;Inherit;False;FlowA;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;47;-1844.899,-677.1729;Inherit;False;FlowB;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.CommentaryNode;75;-729.7758,1064.588;Inherit;False;1460.604;800.1071;;11;83;82;81;80;79;78;77;76;85;86;84;Emissive;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;59;-798.9343,-15.4245;Inherit;False;1460.604;800.1071;;8;67;66;65;64;63;62;61;60;Normal;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;35;-1128.946,-1083.735;Inherit;False;1460.604;800.1071;;8;49;26;27;25;50;58;48;7;Diffuse;1,1,1,1;0;0
Node;AmplifyShaderEditor.TexturePropertyNode;77;-692.5867,1164.693;Inherit;True;Property;_Emissive;Emissive;3;0;Create;True;0;0;0;False;0;False;None;None;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.GetLocalVarNode;78;-664.1678,1390.766;Inherit;False;21;FlowA;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SaturateNode;56;-1582.663,995.3889;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;76;-666.0638,1588.873;Inherit;False;47;FlowB;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TexturePropertyNode;61;-761.7452,84.68105;Inherit;True;Property;_Normal;Normal;4;0;Create;True;0;0;0;False;0;False;None;None;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.GetLocalVarNode;60;-735.2223,508.8612;Inherit;False;47;FlowB;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ColorNode;86;-157.5111,1331.388;Inherit;False;Property;_EmissiveColor;Emissive Color;9;1;[HDR];Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;80;-203.5431,1520.053;Inherit;True;Property;_TextureSample4;Texture Sample 0;4;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;49;-1065.234,-559.4497;Inherit;False;47;FlowB;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;27;-1063.338,-757.5569;Inherit;False;21;FlowA;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;57;-1330.792,998.4604;Inherit;False;BlendTime;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;62;-733.3263,310.7538;Inherit;False;21;FlowA;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;79;-214.9294,1136.718;Inherit;True;Property;_diff2;diff;2;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TexturePropertyNode;26;-1091.757,-983.6295;Inherit;True;Property;_Diffuse; ;2;0;Create;True;0;0;0;False;0;False;None;None;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.SamplerNode;7;-614.0997,-1011.605;Inherit;True;Property;_diff;diff;2;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;48;-600.9673,-764.6996;Inherit;True;Property;_TextureSample0;Texture Sample 0;4;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;81;94.58609,1645.124;Inherit;False;57;BlendTime;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;64;-284.0879,56.70553;Inherit;True;Property;_TextureSample3;Texture Sample 3;2;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;85;132.9604,1360.81;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;84;131.4869,1238.52;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SamplerNode;65;-270.9555,303.6112;Inherit;True;Property;_TextureSample2;Texture Sample 2;4;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;58;-581.3245,-525.0472;Inherit;False;57;BlendTime;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;63;-251.3128,543.2637;Inherit;False;57;BlendTime;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;82;369.4031,1275.863;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.LerpOp;50;-148.436,-851.1375;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.LerpOp;67;181.576,217.1732;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;25;112.6825,-857.1348;Inherit;False;DiffuseA;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;66;442.6944,211.1759;Inherit;False;Normal;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;83;552.8447,1266.593;Inherit;False;Emissive;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;87;1302.325,-227.139;Inherit;False;83;Emissive;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;68;1238.388,-322.2504;Inherit;False;66;Normal;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;28;1251.836,-480.6619;Inherit;False;25;DiffuseA;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;70;1253.887,-33.3701;Inherit;False;Property;_Smoothness;Smoothness;7;0;Create;True;0;0;0;False;0;False;0.5;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;69;1248.407,-141.4985;Inherit;False;Property;_Metalic;Metalic;6;0;Create;True;0;0;0;False;0;False;0.5;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;1632.762,-384.5501;Float;False;True;-1;2;ASEMaterialInspector;0;0;Standard;Maguma;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Back;0;False;-1;0;False;-1;False;0;False;-1;0;False;-1;False;0;Opaque;0.5;True;True;0;False;Opaque;;Geometry;All;18;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;True;0;0;False;-1;0;False;-1;0;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;-1;-1;0;False;-1;0;0;0;False;0.1;False;-1;0;False;-1;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;10;0;9;0
WireConnection;10;1;11;0
WireConnection;39;0;10;0
WireConnection;12;1;10;0
WireConnection;40;1;39;0
WireConnection;41;0;40;0
WireConnection;14;0;12;0
WireConnection;42;0;41;0
WireConnection;15;0;14;0
WireConnection;71;0;15;0
WireConnection;71;1;73;0
WireConnection;72;0;42;0
WireConnection;72;1;73;0
WireConnection;2;0;1;0
WireConnection;29;0;30;0
WireConnection;19;0;71;0
WireConnection;43;0;72;0
WireConnection;17;0;16;0
WireConnection;17;1;2;0
WireConnection;31;0;29;0
WireConnection;18;0;16;0
WireConnection;18;1;17;0
WireConnection;18;2;20;0
WireConnection;44;0;16;0
WireConnection;44;1;17;0
WireConnection;44;2;45;0
WireConnection;53;0;14;0
WireConnection;46;0;44;0
WireConnection;46;1;38;0
WireConnection;37;0;18;0
WireConnection;37;1;38;0
WireConnection;54;0;53;0
WireConnection;55;0;54;0
WireConnection;21;0;37;0
WireConnection;47;0;46;0
WireConnection;56;0;55;0
WireConnection;80;0;77;0
WireConnection;80;1;76;0
WireConnection;57;0;56;0
WireConnection;79;0;77;0
WireConnection;79;1;78;0
WireConnection;7;0;26;0
WireConnection;7;1;27;0
WireConnection;48;0;26;0
WireConnection;48;1;49;0
WireConnection;64;0;61;0
WireConnection;64;1;62;0
WireConnection;85;0;80;0
WireConnection;85;1;86;0
WireConnection;84;0;79;0
WireConnection;84;1;86;0
WireConnection;65;0;61;0
WireConnection;65;1;60;0
WireConnection;82;0;84;0
WireConnection;82;1;85;0
WireConnection;82;2;81;0
WireConnection;50;0;7;0
WireConnection;50;1;48;0
WireConnection;50;2;58;0
WireConnection;67;0;64;0
WireConnection;67;1;65;0
WireConnection;67;2;63;0
WireConnection;25;0;50;0
WireConnection;66;0;67;0
WireConnection;83;0;82;0
WireConnection;0;0;28;0
WireConnection;0;1;68;0
WireConnection;0;2;87;0
WireConnection;0;3;69;0
WireConnection;0;4;70;0
ASEEND*/
//CHKSM=BCE872378F29908ECC3D6DA56FDCF2045426AAD4