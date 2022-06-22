/******************************************************************************
 * Spine Runtimes Software License v2.5
 *
 * Copyright (c) 2013-2016, Esoteric Software
 * All rights reserved.
 *
 * You are granted a perpetual, non-exclusive, non-sublicensable, and
 * non-transferable license to use, install, execute, and perform the Spine
 * Runtimes software and derivative works solely for personal or internal
 * use. Without the written permission of Esoteric Software (see Section 2 of
 * the Spine Software License Agreement), you may not (a) modify, translate,
 * adapt, or develop new applications using the Spine Runtimes or otherwise
 * create derivative works or improvements of the Spine Runtimes or (b) remove,
 * delete, alter, or obscure any trademarks or any copyright, trademark, patent,
 * or other intellectual property or proprietary rights notices on or in the
 * Software, including any copy thereof. Redistributions in binary or source
 * form must include this license and terms.
 *
 * THIS SOFTWARE IS PROVIDED BY ESOTERIC SOFTWARE "AS IS" AND ANY EXPRESS OR
 * IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF
 * MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO
 * EVENT SHALL ESOTERIC SOFTWARE BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES, BUSINESS INTERRUPTION, OR LOSS OF
 * USE, DATA, OR PROFITS) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER
 * IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 *****************************************************************************/

using UnityEngine;
using System.Collections.Generic;

// Compatible with Spine-Unity for Spine 3.6

namespace Spine.Unity.Modules {
	[RequireComponent(typeof(RectTransform))]
	[ExecuteInEditMode]
	public class SkeletonGraphicMultiObject : MonoBehaviour, IAnimationStateComponent, ISkeletonComponent, ISkeletonAnimation, IHasSkeletonDataAsset {

		#region Inspector
		[SerializeField]
		protected SkeletonDataAsset skeletonDataAsset;
		public SkeletonDataAsset SkeletonDataAsset { get { return this.skeletonDataAsset; } set { skeletonDataAsset = value; } }

		[SerializeField]
		[SpineSkin]
		public string initialSkinName;

		[SerializeField]
		[SpineAnimation]
		public string startingAnimation;

		public bool startingLoop;

		public float timeScale = 1f;
		public bool unscaledTime = true;
		public bool freeze;

		public Material material;
		public List<CanvasRenderer> canvasRenderers = new List<CanvasRenderer>();
		#endregion

		public event UpdateBonesDelegate UpdateLocal;
		public event UpdateBonesDelegate UpdateWorld;
		public event UpdateBonesDelegate UpdateComplete;

		public Skeleton Skeleton { get; private set; }
		public AnimationState AnimationState { get; private set; }

		[SerializeField] protected MeshGenerator meshGenerator = new MeshGenerator();
		public MeshGenerator MeshGenerator { get { return meshGenerator; } }
		readonly SkeletonRendererInstruction currentInstruction = new SkeletonRendererInstruction();
		readonly ExposedList<Mesh> meshes = new ExposedList<Mesh>();

		public bool IsValid { get { return Skeleton != null; } }

		Canvas canvas;
		public Canvas Canvas {
			get {
				if (canvas == null) {
					canvas = GetComponentsInParent<Canvas>(false)[0];
				}
				return canvas;
			}
		}

		public Bounds GetMeshBounds () {
			Initialize(false);
			UpdateMesh();
			int submeshCount = currentInstruction.submeshInstructions.Count;

			Bounds bounds = default(Bounds);
			var meshesItems = meshes.Items;
			for (int i = 0; i < submeshCount; i++) {
				meshesItems[i].RecalculateBounds();
				bounds.Encapsulate(meshesItems[i].bounds);
			}

			return bounds;
		}

		void OnValidate () {
			if (!IsValid) return;
			this.Update(0f);
			this.LateUpdate();
		}

		void Awake () {
			if (!IsValid) Initialize(false);
		}

		public void Clear () {
			Skeleton = null;
			foreach (var cr in canvasRenderers)
				cr.Clear();

			foreach (var m in meshes)
				Destroy(m);
			
			meshes.Clear();
		}

		public void TrimRenderers () {
			var newList = new List<CanvasRenderer>();
			foreach (var cr in canvasRenderers) {
				if (cr.gameObject.activeSelf) {
					newList.Add(cr);
				} else {
					if (Application.isEditor && !Application.isPlaying)
						DestroyImmediate(cr);
					else
						Destroy(cr);
				}
			}

			canvasRenderers = newList;
		}

		public void Update () {
			if (freeze) return;
			Update(unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
		}

		public void Update (float deltaTime) {
			if (!this.IsValid) return;

			deltaTime *= timeScale;
			Skeleton.Update(deltaTime);
			AnimationState.Update(deltaTime);
			AnimationState.Apply(Skeleton);

			if (UpdateLocal != null) UpdateLocal(this);

			Skeleton.UpdateWorldTransform();

			if (UpdateWorld != null) { 
				UpdateWorld(this);
				Skeleton.UpdateWorldTransform();
			}

			if (UpdateComplete != null) UpdateComplete(this);
		}

		public void LateUpdate () {
			if (freeze) return;
			UpdateMesh();
		}

		public void Initialize (bool overwrite) {
			if (IsValid && !overwrite) return; // Obey overwrite parameter.

			// Try to load SkeletonData. Fail the rest of initialization if it can't load.
			if (this.skeletonDataAsset == null) return;
			var skeletonData = this.skeletonDataAsset.GetSkeletonData(false);
			if (skeletonData == null) return;

			if (skeletonDataAsset.atlasAssets.Length <= 0 || skeletonDataAsset.atlasAssets[0].materials.Length <= 0) return;

			this.AnimationState = new AnimationState(skeletonDataAsset.GetAnimationStateData());
			if (AnimationState == null) {
				Clear();
				return;
			}

			this.Skeleton = new Skeleton(skeletonData);

			// Set the initial Skin and Animation
			if (!string.IsNullOrEmpty(initialSkinName))
				Skeleton.SetSkin(initialSkinName);

			#if UNITY_EDITOR
			if (!string.IsNullOrEmpty(startingAnimation)) {
				if (Application.isPlaying) {
					AnimationState.SetAnimation(0, startingAnimation, startingLoop);
				} else {
					// Assume SkeletonAnimation is valid for skeletonData and skeleton. Checked above.
					var animationObject = skeletonDataAsset.GetSkeletonData(false).FindAnimation(startingAnimation);
					if (animationObject != null)
						animationObject.PoseSkeleton(Skeleton, 0);
				}
				Update(0);
			}
			#else
			if (!string.IsNullOrEmpty(startingAnimation)) {
				AnimationState.SetAnimation(0, startingAnimation, startingLoop);
				Update(0);
			}
			#endif
		}

		public void UpdateMesh () {
			if (this.IsValid) {
				MeshGenerator.GenerateSkeletonRendererInstruction(currentInstruction, Skeleton, null, null, false);

				// Ensure canvas renderer count.
				int submeshCount = currentInstruction.submeshInstructions.Count;
				int rendererCount = canvasRenderers.Count;
				for (int i = rendererCount; i < submeshCount; i++) {
					var go = new GameObject(string.Format("Renderer[{0}]", i));
					go.transform.SetParent(this.transform, false);
					//go.hideFlags = HideFlags.NotEditable;
					var cr = go.AddComponent<CanvasRenderer>();
					canvasRenderers.Add(cr);
				}

				var c = Canvas;
				float scale = (c == null) ? 100 : c.referencePixelsPerUnit;

				// Ensure meshes count
				int oldCount = meshes.Count;
				meshes.EnsureCapacity(submeshCount);
				var meshesItems = meshes.Items;
				for (int i = oldCount; i < submeshCount; i++)
					if (meshesItems[i] == null) meshesItems[i] = new Mesh();

				// Generate meshes.
				for (int i = 0; i < submeshCount; i++) {
					meshGenerator.Begin();
					meshGenerator.AddSubmesh(currentInstruction.submeshInstructions.Items[i]);

					var mesh = meshesItems[i];
					meshGenerator.ScaleVertexData(scale);
					meshGenerator.FillVertexData(mesh);
					meshGenerator.FillTrianglesSingle(mesh);
					meshGenerator.FillLateVertexData(mesh);

					var submeshMaterial = currentInstruction.submeshInstructions.Items[i].material;
					var canvasRenderer = canvasRenderers[i];
					canvasRenderer.gameObject.SetActive(true);
					canvasRenderer.SetMesh(mesh);
					canvasRenderer.materialCount = 1;
					canvasRenderer.SetMaterial(material, submeshMaterial.mainTexture);
				}

				// Disable extra.
				rendererCount = canvasRenderers.Count;
				for (int i = submeshCount; i < rendererCount; i++) {
					canvasRenderers[i].Clear();
					canvasRenderers[i].gameObject.SetActive(false);
				}
					
			}
		}

	}
}
