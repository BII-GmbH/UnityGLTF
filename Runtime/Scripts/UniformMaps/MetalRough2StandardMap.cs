using UnityEngine;

namespace UnityGLTF
{
	public class MetalRough2StandardMap : StandardMap, IMetalRoughUniformMap
	{
		private Vector2 baseColorOffset = new Vector2(0, 0);
		private static readonly int ColorId = Shader.PropertyToID("_Color");
		private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
		private static readonly int BaseColorMapId = Shader.PropertyToID("_BaseColorMap");
		private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

		public MetalRough2StandardMap(int MaxLOD = 1000) : base("Standard", null, MaxLOD) { }
		protected MetalRough2StandardMap(string shaderName, string shaderGuid, int MaxLOD = 1000) : base(shaderName, shaderGuid, MaxLOD) { }
		protected MetalRough2StandardMap(Material m, int MaxLOD = 1000) : base(m, MaxLOD) { }

		public virtual Texture BaseColorTexture
		{
			get { 
				if (_material.HasProperty("_BaseColorMap")) {
					return _material.GetTexture(BaseColorMapId);
				}
				
				return _material.GetTexture(MainTexId);
			}
			set {
				if (_material.HasProperty("_BaseColorMap")) {
					_material.SetTexture(BaseColorMapId, value);
				}
				else if (_material.HasProperty("_MainTex")) {
					_material.SetTexture(MainTexId, value);
				}
			}
		}

		// not implemented by the Standard shader
		public virtual int BaseColorTexCoord
		{
			get { return 0; }
			set { return; }
		}

		public virtual Vector2 BaseColorXOffset
		{
			get { return baseColorOffset; }
			set {
				baseColorOffset = value;
				
				if (_material.HasProperty("_BaseColorMap")) {
					_material.SetTextureOffset(BaseColorMapId, value);
				}
				else if (_material.HasProperty("_MainTex")) {
					_material.SetTextureOffset(MainTexId, value);
				}
			}
		}

		public virtual double BaseColorXRotation
		{
			get { return 0; }
			set { return; }
		}

		public virtual Vector2 BaseColorXScale
		{
			get {
				if (_material.HasProperty("_BaseColorMap")) {
					return _material.GetTextureScale(BaseColorMapId);
				}
				
				return _material.GetTextureScale(MainTexId);
			}
			set {
				if (_material.HasProperty("_BaseColorMap")) {
					_material.SetTextureScale(BaseColorMapId, value);
				}
				else if (_material.HasProperty("_MainTex")) {
					_material.SetTextureScale(MainTexId, value);
				}
				
				BaseColorXOffset = baseColorOffset;
			}
		}

		public virtual int BaseColorXTexCoord
		{
			get { return 0; }
			set { return; }
		}

		public virtual Color BaseColorFactor
		{
			get {
				if (_material.HasProperty("_BaseColor")){
					return _material.GetColor(BaseColorId);
				}
				else if (_material.HasProperty("_Color")) {
					return _material.GetColor(ColorId);
				}
				return Color.white;
			}
			set {
				if (_material.HasProperty("_BaseColor")){
					_material.SetColor(BaseColorId, value);
				}
				else if (_material.HasProperty("_Color")) {
					_material.SetColor(ColorId, value);
				}
			}
		}

		public virtual Texture MetallicRoughnessTexture
		{
			get { return null; }
			set
			{
				// cap metalness at 0.5 to compensate for lack of texture
				MetallicFactor = Mathf.Min(0.5f, (float)MetallicFactor);
			}
		}

		// not implemented by the Standard shader
		public virtual int MetallicRoughnessTexCoord
		{
			get { return 0; }
			set { return; }
		}

		public virtual Vector2 MetallicRoughnessXOffset
		{
			get { return new Vector2(0, 0); }
			set { return; }
		}

		public virtual double MetallicRoughnessXRotation
		{
			get { return 0; }
			set { return; }
		}

		public virtual Vector2 MetallicRoughnessXScale
		{
			get { return new Vector2(1, 1); }
			set { return; }
		}

		public virtual int MetallicRoughnessXTexCoord
		{
			get { return 0; }
			set { return; }
		}

		public virtual double MetallicFactor
		{
			get { return _material.GetFloat("_Metallic"); }
			set { _material.SetFloat("_Metallic", (float)value); }
		}

		// not supported by the Standard shader
		public virtual double RoughnessFactor
		{
			get { return 0.5; }
			set { return; }
		}

		public override IUniformMap Clone()
		{
			var copy = new MetalRough2StandardMap(new Material(_material));
			base.Copy(copy);
			return copy;
		}
	}
}
