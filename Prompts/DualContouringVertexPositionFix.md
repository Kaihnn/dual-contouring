# Diagnostic Dual Contouring - Positionnement des Vertices

## Problème Résolu

Le problème de positionnement incorrect des vertices a été corrigé avec les améliorations suivantes :

### 1. **Correction du QEF (Quadratic Error Function)**
- **Avant** : Le QEF calculait directement `A^T * A * x = A^T * b` sans point de référence
- **Après** : Le QEF utilise maintenant le centre de masse comme référence avec `A^T * A * offset = A^T * b`
  - Calcul de l'offset par rapport au `massPoint` (centre de masse des intersections)
  - Ajout d'une régularisation (0.001) pour éviter les matrices singulières
  - Validation du résultat avant application

### 2. **Amélioration de la Contrainte de Position**
- **Avant** : Clamping simple qui pouvait forcer le vertex dans un coin
- **Après** : 
  - Vérification de distance au centre de masse (max 2x la taille de cellule)
  - Fallback vers le centre de masse si le QEF donne une solution aberrante
  - Détection des NaN et Inf
  - Clamping final dans les limites de la cellule

### 3. **Options de Debug Améliorées**

Nouvelles options de visualisation disponibles dans `DualContouringVisualizationOptions` :

- **DrawEdgeIntersections** : Affiche les intersections d'arêtes (points rouges) et leurs normales (lignes cyan)
- **DrawMassPoint** : Affiche le centre de masse des intersections (sphère bleue) avec des lignes vers chaque intersection

## Comment Utiliser le Debug

1. **Ajouter le component** `DualContouringVisualizationOptionsAuthoring` à un GameObject
2. **Activer les options** :
   - `Enabled = true` : Active la visualisation
   - `DrawEmptyCell = false` : Masque les cellules vides
   - `DrawEdgeIntersections = true` : Affiche les intersections d'arêtes
   - `DrawMassPoint = true` : Affiche le centre de masse

3. **Légende des couleurs** :
   - 🟢 **Vert** : Wireframe de la cellule avec vertex
   - 🟡 **Jaune** : Position du vertex calculé
   - 🟣 **Magenta** : Normale de la cellule
   - 🔴 **Rouge** : Points d'intersection sur les arêtes
   - 🔵 **Cyan** : Normales aux points d'intersection
   - 🔵 **Bleu** : Centre de masse des intersections (massPoint)
   - 🟦 **Bleu clair** : Lignes du centre de masse vers les intersections

## Diagnostic

Si un vertex est mal positionné :

1. **Vérifier les intersections** (points rouges) :
   - Sont-elles toutes regroupées dans un coin ? → Problème de calcul d'intersection
   - Sont-elles bien réparties ? → Le QEF devrait bien fonctionner

2. **Vérifier le centre de masse** (sphère bleue) :
   - Est-il au centre des intersections ?
   - Le vertex (jaune) est-il proche du centre de masse ?

3. **Vérifier les normales** (lignes cyan) :
   - Pointent-elles dans des directions cohérentes ?
   - Sont-elles perpendiculaires à la surface ?

## Paramètres Ajustables

Dans `DualContouringSystem.cs` → `CalculateVertexPositionAndNormal` :

```csharp
float maxDistance = cellSize * 2.0f; // Distance max du vertex au centre de masse
```

Dans `DualContouringSystem.cs` → `SolveQef` :

```csharp
float regularization = 0.001f; // Régularisation pour stabilité numérique
```

## Architecture du QEF

Le QEF minimise la somme des distances au carré entre le vertex et les plans définis par chaque intersection :

```
Minimiser : Σ (n_i · (x - p_i))²

Équivalent à résoudre : A^T * A * (x - massPoint) = A^T * b
où b_i = n_i · (p_i - massPoint)
```

Cette formulation garantit que :
- Le vertex est proche de tous les plans d'intersection
- La solution est stable numériquement
- Le vertex reste proche du centre de masse des intersections

