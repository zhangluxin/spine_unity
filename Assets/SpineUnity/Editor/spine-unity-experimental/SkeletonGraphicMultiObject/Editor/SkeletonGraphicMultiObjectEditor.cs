using UnityEngine;
using Spine.Unity.Modules;
using UnityEditor;

namespace Spine.Unity.Editor {
	
	[InitializeOnLoad]
	[CustomEditor(typeof(SkeletonGraphicMultiObject))]
	public class SkeletonGraphicMultiObjectEditor : UnityEditor.Editor {

		public override void OnInspectorGUI () {
			var so = serializedObject;
			var sda = so.FindProperty("skeletonDataAsset");

			EditorGUI.BeginChangeCheck();

			EditorGUILayout.PropertyField(sda);
			var skeletonGraphic = target as SkeletonGraphicMultiObject;

			if (skeletonGraphic.SkeletonDataAsset == null) {
				EditorGUILayout.HelpBox("You need to assign a SkeletonDataAsset first.", MessageType.Info);
				skeletonGraphic.Clear();
				serializedObject.ApplyModifiedProperties();
				serializedObject.Update();
				return;
			}

			if (EditorGUI.EndChangeCheck()) {				
				bool notNull = skeletonGraphic.SkeletonDataAsset != null;
				if (notNull)
					skeletonGraphic.Initialize(false);

				serializedObject.ApplyModifiedProperties();
				serializedObject.Update();

				if (notNull && skeletonGraphic.IsValid && ((SkeletonDataAsset)sda.objectReferenceValue).GetSkeletonData(true) != skeletonGraphic.Skeleton.Data) {
					skeletonGraphic.Clear();
					skeletonGraphic.initialSkinName = string.Empty;
					skeletonGraphic.Initialize(true);
					skeletonGraphic.Update(0);
					skeletonGraphic.LateUpdate();
				} 
			}

			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField(so.FindProperty("initialSkinName"));
			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(so.FindProperty("meshGenerator").FindPropertyRelative("settings"), new GUIContent("Mesh Generator Settings"), true);
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);

			if (!string.IsNullOrEmpty(skeletonGraphic.startingAnimation) && skeletonGraphic.SkeletonDataAsset.GetSkeletonData(true).FindAnimation(skeletonGraphic.startingAnimation) == null) {
				skeletonGraphic.startingAnimation = string.Empty;
			}
				
			EditorGUILayout.PropertyField(so.FindProperty("startingAnimation"));
			EditorGUILayout.PropertyField(so.FindProperty("startingLoop"));
			EditorGUILayout.PropertyField(so.FindProperty("timeScale"));
			EditorGUILayout.PropertyField(so.FindProperty("unscaledTime"), new GUIContent("Use Unscaled Time", "If checked, this will use Time.unscaledDeltaTime to make this update independent of game Time.timeScale. Instance SkeletonGraphic.timeScale will still be applied."));
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("UI", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(so.FindProperty("freeze"));
			EditorGUILayout.PropertyField(so.FindProperty("material"));
			EditorGUILayout.PropertyField(so.FindProperty("canvasRenderers"), true);

			if (SpineInspectorUtility.CenteredButton(new GUIContent("Trim Renderers", "Remove currently unused CanvasRenderer GameObjects. These will be regenerated whenever needed.")))
				skeletonGraphic.TrimRenderers();

			bool wasChanged = EditorGUI.EndChangeCheck();

			if (wasChanged)
				serializedObject.ApplyModifiedProperties();
		}

		#region Menus
		[MenuItem("CONTEXT/SkeletonGraphicMultiObject/Match RectTransform with Mesh Bounds")]
		static void MatchRectTransformWithBounds (MenuCommand command) {
			var skeletonGraphic = (SkeletonGraphicMultiObject)command.context;
			var bounds = skeletonGraphic.GetMeshBounds();
			var size = bounds.size;
			var center = bounds.center;

			Vector2 p;
			if (size.x == 0 || size.y == 0) {
				p = new Vector2(0.5f, 0.5f);
				size = Vector2.one * 100f;
			} else {
				p = new Vector2(
					0.5f - (center.x / size.x),
					0.5f - (center.y / size.y)
				);
			}


			var rectTransform = skeletonGraphic.transform as RectTransform;
			rectTransform.sizeDelta = size;
			rectTransform.pivot = p;
		}

		[MenuItem("GameObject/Spine/SkeletonGraphic MultiObject (UnityUI)", false, 15)]
		static public void SkeletonGraphicCreateMenuItem () {
			var parentGameObject = Selection.activeObject as GameObject;
			var parentTransform = parentGameObject == null ? null : parentGameObject.GetComponent<RectTransform>();

			if (parentTransform == null)
				Debug.LogWarning("Your new SkeletonGraphic will not be visible until it is placed under a Canvas");

			var gameObject = NewSkeletonGraphicMultiObjectGameObject("New SkeletonGraphicMultiObject");
			gameObject.transform.SetParent(parentTransform, false);
			EditorUtility.FocusProjectWindow();
			Selection.activeObject = gameObject;
			EditorGUIUtility.PingObject(Selection.activeObject);
		}

//		// SpineEditorUtilities.InstantiateDelegate. Used by drag and drop.
//		public static Component SpawnSkeletonGraphicFromDrop (SkeletonDataAsset data) {
//			return InstantiateSkeletonGraphic(data);
//		}
//
//		public static SkeletonGraphicMultiObject InstantiateSkeletonGraphic (SkeletonDataAsset skeletonDataAsset, string skinName) {
//			return InstantiateSkeletonGraphic(skeletonDataAsset, skeletonDataAsset.GetSkeletonData(true).FindSkin(skinName));
//		}
//
//		public static SkeletonGraphicMultiObject InstantiateSkeletonGraphic (SkeletonDataAsset skeletonDataAsset, Skin skin = null) {
//			string spineGameObjectName = string.Format("SkeletonGraphic ({0})", skeletonDataAsset.name.Replace("_SkeletonData", ""));
//			var go = NewSkeletonGraphicMultiObjectGameObject(spineGameObjectName);
//			var graphic = go.GetComponent<SkeletonGraphicMultiObject>();
//			graphic.SkeletonDataAsset = skeletonDataAsset;
//
//			SkeletonData data = skeletonDataAsset.GetSkeletonData(true);
//
//			if (data == null) {
//				for (int i = 0; i < skeletonDataAsset.atlasAssets.Length; i++) {
//					string reloadAtlasPath = AssetDatabase.GetAssetPath(skeletonDataAsset.atlasAssets[i]);
//					skeletonDataAsset.atlasAssets[i] = (AtlasAsset)AssetDatabase.LoadAssetAtPath(reloadAtlasPath, typeof(AtlasAsset));
//				}
//
//				data = skeletonDataAsset.GetSkeletonData(true);
//			}
//
//			if (skin == null)
//				skin = data.DefaultSkin;
//
//			if (skin == null)
//				skin = data.Skins.Items[0];
//
//			graphic.Initialize(false);
//			graphic.Skeleton.SetSkin(skin);
//			graphic.initialSkinName = skin.Name;
//			graphic.Skeleton.UpdateWorldTransform();
//			graphic.UpdateMesh();
//
//			return graphic;
//		}

		static GameObject NewSkeletonGraphicMultiObjectGameObject (string gameObjectName) {
			var go = new GameObject(gameObjectName, typeof(RectTransform), typeof(SkeletonGraphicMultiObject));
			var graphic = go.GetComponent<SkeletonGraphicMultiObject>();
			graphic.material = SkeletonGraphicInspector.DefaultSkeletonGraphicMaterial;
			return go;
		}

		#endregion
	}

}