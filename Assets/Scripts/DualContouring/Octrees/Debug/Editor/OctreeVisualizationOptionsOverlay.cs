using Unity.Entities;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace DualContouring.Octrees.Debug.Editor
{
    [Overlay(typeof(SceneView), "Octree Visualization")]
    public class OctreeVisualizationOptionsOverlay : Overlay
    {
        private VisualElement _root;
        private Toggle _enabledToggle;

        public override VisualElement CreatePanelContent()
        {
            _root = new VisualElement();
            _root.style.paddingLeft = 10;
            _root.style.paddingRight = 10;
            _root.style.paddingTop = 10;
            _root.style.paddingBottom = 10;
            _root.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.95f);
            _root.style.borderBottomLeftRadius = 5;
            _root.style.borderBottomRightRadius = 5;
            _root.style.borderTopLeftRadius = 5;
            _root.style.borderTopRightRadius = 5;
            _root.style.minWidth = 250;

            // Header
            var headerLabel = new Label("Visualization Options");
            headerLabel.style.fontSize = 16;
            headerLabel.style.color = Color.white;
            headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            headerLabel.style.marginBottom = 10;
            _root.Add(headerLabel);

            // Toggle pour Enabled
            _enabledToggle = new Toggle("Enable Visualization");
            _enabledToggle.style.color = Color.white;
            _enabledToggle.style.fontSize = 12;
            _enabledToggle.RegisterValueChangedCallback(evt => OnEnabledChanged(evt.newValue));
            _root.Add(_enabledToggle);

            // S'abonner aux mises à jour
            EditorApplication.update += OnEditorUpdate;

            // Initialiser la valeur
            UpdateToggleValue();

            return _root;
        }

        public override void OnWillBeDestroyed()
        {
            base.OnWillBeDestroyed();
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (_enabledToggle != null)
            {
                UpdateToggleValue();
            }
        }

        private void UpdateToggleValue()
        {
            if (!Application.isPlaying || World.DefaultGameObjectInjectionWorld == null)
            {
                // En mode édition, désactiver le toggle
                _enabledToggle.SetEnabled(false);
                _enabledToggle.SetValueWithoutNotify(false);
                return;
            }

            // En mode Play, activer le toggle
            _enabledToggle.SetEnabled(true);

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            
            // Chercher le singleton via une requête
            EntityQuery query = entityManager.CreateEntityQuery(typeof(OctreeVisualizationOptions));
            Unity.Collections.NativeArray<Entity> entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            if (entities.Length > 0)
            {
                var options = entityManager.GetComponentData<OctreeVisualizationOptions>(entities[0]);
                _enabledToggle.SetValueWithoutNotify(options.Enabled);
            }
            
            entities.Dispose();
        }

        private void OnEnabledChanged(bool newValue)
        {
            if (!Application.isPlaying || World.DefaultGameObjectInjectionWorld == null)
                return;

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            // Chercher le singleton via une requête
            EntityQuery query = entityManager.CreateEntityQuery(typeof(OctreeVisualizationOptions));
            Unity.Collections.NativeArray<Entity> entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            
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
    }
}

