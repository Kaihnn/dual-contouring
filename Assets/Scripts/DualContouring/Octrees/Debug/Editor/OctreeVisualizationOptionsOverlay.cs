using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace DualContouring.Octrees.Debug.Editor
{
    [Overlay(typeof(SceneView), "Octree Visualization")]
    public class OctreeVisualizationOptionsOverlay : Overlay
    {
        private Label depthLabel;
        private MinMaxSlider depthSlider;
        private Toggle enabledToggle;
        private VisualElement root;

        public override VisualElement CreatePanelContent()
        {
            root = new VisualElement();
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;
            root.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.95f);
            root.style.borderBottomLeftRadius = 5;
            root.style.borderBottomRightRadius = 5;
            root.style.borderTopLeftRadius = 5;
            root.style.borderTopRightRadius = 5;
            root.style.minWidth = 250;

            // Header
            var headerLabel = new Label("Visualization Options");
            headerLabel.style.fontSize = 16;
            headerLabel.style.color = Color.white;
            headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            headerLabel.style.marginBottom = 10;
            root.Add(headerLabel);

            // Toggle pour Enabled
            enabledToggle = new Toggle("Enable Visualization");
            enabledToggle.style.color = Color.white;
            enabledToggle.style.fontSize = 12;
            enabledToggle.RegisterValueChangedCallback(evt => OnEnabledChanged(evt.newValue));
            root.Add(enabledToggle);

            root.Add(CreateSpacer());

            depthLabel = new Label("Depth Range: [0, 5]");
            depthLabel.style.color = Color.white;
            depthLabel.style.fontSize = 12;
            depthLabel.style.marginTop = 5;
            root.Add(depthLabel);

            depthSlider = new MinMaxSlider("Depth", 0, 5, 0, 10);
            depthSlider.style.color = Color.white;
            depthSlider.style.marginTop = 5;
            depthSlider.RegisterValueChangedCallback(evt => OnDepthChanged(evt.newValue));
            root.Add(depthSlider);

            // S'abonner aux mises à jour
            EditorApplication.update += OnEditorUpdate;

            // Initialiser la valeur
            UpdateToggleValue();
            UpdateDepthSlider();

            return root;
        }

        public override void OnWillBeDestroyed()
        {
            base.OnWillBeDestroyed();
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (enabledToggle != null)
            {
                UpdateToggleValue();
                UpdateDepthSlider();
            }
        }

        private void UpdateToggleValue()
        {
            if (!Application.isPlaying || World.DefaultGameObjectInjectionWorld == null)
            {
                // En mode édition, désactiver le toggle
                enabledToggle.SetEnabled(false);
                enabledToggle.SetValueWithoutNotify(false);
                depthSlider.SetEnabled(false);
                return;
            }

            // En mode Play, activer le toggle
            enabledToggle.SetEnabled(true);
            depthSlider.SetEnabled(true);

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            // Chercher le singleton via une requête
            EntityQuery query = entityManager.CreateEntityQuery(typeof(OctreeVisualizationOptions));
            NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);

            if (entities.Length > 0)
            {
                var options = entityManager.GetComponentData<OctreeVisualizationOptions>(entities[0]);
                enabledToggle.SetValueWithoutNotify(options.Enabled);
            }

            entities.Dispose();
        }

        private void OnEnabledChanged(bool newValue)
        {
            if (!Application.isPlaying || World.DefaultGameObjectInjectionWorld == null)
            {
                return;
            }

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            // Chercher le singleton via une requête
            EntityQuery query = entityManager.CreateEntityQuery(typeof(OctreeVisualizationOptions));
            NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);

            if (entities.Length > 0)
            {
                var options = entityManager.GetComponentData<OctreeVisualizationOptions>(entities[0]);
                options.Enabled = newValue;
                entityManager.SetComponentData(entities[0], options);

                // Rafraîchir la SceneView
                SceneView.RepaintAll();
            }

            entities.Dispose();
        }

        private void UpdateDepthSlider()
        {
            if (!Application.isPlaying || World.DefaultGameObjectInjectionWorld == null)
            {
                return;
            }

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            EntityQuery query = entityManager.CreateEntityQuery(typeof(OctreeVisualizationOptions));
            NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);

            if (entities.Length > 0)
            {
                var options = entityManager.GetComponentData<OctreeVisualizationOptions>(entities[0]);
                depthSlider.SetValueWithoutNotify(new Vector2(options.Depth.x, options.Depth.y));
                depthLabel.text = $"Depth Range: [{options.Depth.x:F0}, {options.Depth.y:F0}]";
            }

            entities.Dispose();
        }

        private void OnDepthChanged(Vector2 newValue)
        {
            if (!Application.isPlaying || World.DefaultGameObjectInjectionWorld == null)
            {
                return;
            }

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            EntityQuery query = entityManager.CreateEntityQuery(typeof(OctreeVisualizationOptions));
            NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);

            if (entities.Length > 0)
            {
                var options = entityManager.GetComponentData<OctreeVisualizationOptions>(entities[0]);
                options.Depth = new int2((int)newValue.x, (int)newValue.y);
                entityManager.SetComponentData(entities[0], options);

                depthLabel.text = $"Depth Range: [{newValue.x:F0}, {newValue.y:F0}]";

                SceneView.RepaintAll();
            }

            entities.Dispose();
        }

        private VisualElement CreateSpacer()
        {
            var spacer = new VisualElement();
            spacer.style.height = 10;
            return spacer;
        }
    }
}