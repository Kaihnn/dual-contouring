using Unity.Entities;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace DualContouring.DualContouring.Debug.Editor
{
    /// <summary>
    /// Overlay pour contrôler les options de visualisation Dual Contouring dans la SceneView
    /// Compatible Unity 2021.2+
    /// </summary>
    [Overlay(typeof(SceneView), "Dual Contouring Visualization")]
    public class DualContouringVisualizationOptionsOverlay : Overlay
    {
        private VisualElement _root;
        private Toggle _enabledToggle;
        private Toggle _drawEmptyCellToggle;
        private Toggle _drawEdgeIntersectionsToggle;
        private Toggle _drawMassPointToggle;

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

            // Toggle pour DrawEmptyCell
            _drawEmptyCellToggle = new Toggle("Draw Empty Cells");
            _drawEmptyCellToggle.style.color = Color.white;
            _drawEmptyCellToggle.style.fontSize = 12;
            _drawEmptyCellToggle.RegisterValueChangedCallback(evt => OnDrawEmptyCellChanged(evt.newValue));
            _root.Add(_drawEmptyCellToggle);

            // Toggle pour DrawEdgeIntersections
            _drawEdgeIntersectionsToggle = new Toggle("Draw Edge Intersections");
            _drawEdgeIntersectionsToggle.style.color = Color.white;
            _drawEdgeIntersectionsToggle.style.fontSize = 12;
            _drawEdgeIntersectionsToggle.RegisterValueChangedCallback(evt => OnDrawEdgeIntersectionsChanged(evt.newValue));
            _root.Add(_drawEdgeIntersectionsToggle);

            // Toggle pour DrawMassPoint
            _drawMassPointToggle = new Toggle("Draw Mass Point");
            _drawMassPointToggle.style.color = Color.white;
            _drawMassPointToggle.style.fontSize = 12;
            _drawMassPointToggle.RegisterValueChangedCallback(evt => OnDrawMassPointChanged(evt.newValue));
            _root.Add(_drawMassPointToggle);

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
            if (_enabledToggle != null && _drawEmptyCellToggle != null && 
                _drawEdgeIntersectionsToggle != null && _drawMassPointToggle != null)
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
                _drawEmptyCellToggle.SetEnabled(false);
                _drawEmptyCellToggle.SetValueWithoutNotify(false);
                _drawEdgeIntersectionsToggle.SetEnabled(false);
                _drawEdgeIntersectionsToggle.SetValueWithoutNotify(false);
                _drawMassPointToggle.SetEnabled(false);
                _drawMassPointToggle.SetValueWithoutNotify(false);
                return;
            }

            // En mode Play, activer le toggle
            _enabledToggle.SetEnabled(true);
            _drawEmptyCellToggle.SetEnabled(true);
            _drawEdgeIntersectionsToggle.SetEnabled(true);
            _drawMassPointToggle.SetEnabled(true);

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            
            // Chercher le singleton via une requête
            EntityQuery query = entityManager.CreateEntityQuery(typeof(DualContouringVisualizationOptions));
            Unity.Collections.NativeArray<Entity> entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            if (entities.Length > 0)
            {
                var options = entityManager.GetComponentData<DualContouringVisualizationOptions>(entities[0]);
                _enabledToggle.SetValueWithoutNotify(options.Enabled);
                _drawEmptyCellToggle.SetValueWithoutNotify(options.DrawEmptyCell);
                _drawEdgeIntersectionsToggle.SetValueWithoutNotify(options.DrawEdgeIntersections);
                _drawMassPointToggle.SetValueWithoutNotify(options.DrawMassPoint);
            }
            
            entities.Dispose();
        }

        private void OnEnabledChanged(bool newValue)
        {
            if (!Application.isPlaying || World.DefaultGameObjectInjectionWorld == null)
                return;

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            // Chercher le singleton via une requête
            EntityQuery query = entityManager.CreateEntityQuery(typeof(DualContouringVisualizationOptions));
            Unity.Collections.NativeArray<Entity> entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            if (entities.Length > 0)
            {
                var options = entityManager.GetComponentData<DualContouringVisualizationOptions>(entities[0]);
                options.Enabled = newValue;
                entityManager.SetComponentData(entities[0], options);
                
                // Rafraîchir la SceneView
                SceneView.RepaintAll();
            }
            
            entities.Dispose();
        }

        private void OnDrawEmptyCellChanged(bool newValue)
        {
            if (!Application.isPlaying || World.DefaultGameObjectInjectionWorld == null)
                return;

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            // Chercher le singleton via une requête
            EntityQuery query = entityManager.CreateEntityQuery(typeof(DualContouringVisualizationOptions));
            Unity.Collections.NativeArray<Entity> entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            if (entities.Length > 0)
            {
                var options = entityManager.GetComponentData<DualContouringVisualizationOptions>(entities[0]);
                options.DrawEmptyCell = newValue;
                entityManager.SetComponentData(entities[0], options);
                
                // Rafraîchir la SceneView
                SceneView.RepaintAll();
            }
            
            entities.Dispose();
        }

        private void OnDrawEdgeIntersectionsChanged(bool newValue)
        {
            if (!Application.isPlaying || World.DefaultGameObjectInjectionWorld == null)
                return;

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            // Chercher le singleton via une requête
            EntityQuery query = entityManager.CreateEntityQuery(typeof(DualContouringVisualizationOptions));
            Unity.Collections.NativeArray<Entity> entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            if (entities.Length > 0)
            {
                var options = entityManager.GetComponentData<DualContouringVisualizationOptions>(entities[0]);
                options.DrawEdgeIntersections = newValue;
                entityManager.SetComponentData(entities[0], options);
                
                // Rafraîchir la SceneView
                SceneView.RepaintAll();
            }
            
            entities.Dispose();
        }

        private void OnDrawMassPointChanged(bool newValue)
        {
            if (!Application.isPlaying || World.DefaultGameObjectInjectionWorld == null)
                return;

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            // Chercher le singleton via une requête
            EntityQuery query = entityManager.CreateEntityQuery(typeof(DualContouringVisualizationOptions));
            Unity.Collections.NativeArray<Entity> entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            if (entities.Length > 0)
            {
                var options = entityManager.GetComponentData<DualContouringVisualizationOptions>(entities[0]);
                options.DrawMassPoint = newValue;
                entityManager.SetComponentData(entities[0], options);
                
                // Rafraîchir la SceneView
                SceneView.RepaintAll();
            }
            
            entities.Dispose();
        }
    }
}

