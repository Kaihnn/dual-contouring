# Dual Contouring via Octree - Architecture Complète

## Vue d'ensemble

Le pipeline de génération de mesh par Dual Contouring se compose de **3 couches distinctes** avec des responsabilités bien séparées :

```
┌─────────────────────────────────────────────────────────────────────┐
│                         SCALAR FIELD                                │
│  Grille 3D de valeurs représentant une surface implicite            │
│  Chaque point = distance signée à la surface (négatif = intérieur)  │
└─────────────────────────────────────────────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────────┐
│                            OCTREE                                   │
│  Représentation hiérarchique compacte du scalar field               │
│  Subdivise uniquement les régions avec variation/surface            │
└─────────────────────────────────────────────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────────┐
│                     DUAL CONTOURING CELLS                           │
│  Cellules contenant un vertex de surface                            │
│  Position optimisée via QEF, normale calculée                       │
└─────────────────────────────────────────────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────────┐
│                            MESH                                     │
│  Vertices + Triangles pour rendu Unity                              │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 1. Scalar Field (Champ Scalaire)

### Concept

Le scalar field est une **grille 3D régulière** où chaque point stocke une valeur représentant la **distance signée** à la surface implicite :

- **Valeur < 0** : Point à l'intérieur de la surface (matière)
- **Valeur > 0** : Point à l'extérieur de la surface (vide)
- **Valeur = 0** : Point exactement sur la surface

### Structures de données

```csharp
// Valeur en un point de la grille
public struct ScalarFieldItem : IBufferElementData
{
    public float Value;  // Distance signée à la surface
}

// Métadonnées de la grille
public struct ScalarFieldInfos : IComponentData
{
    public int3 GridSize;            // Dimensions (ex: 33x33x33)
    public float CellSize;           // Taille d'une cellule en unités monde
    public float3 ScalarFieldOffset; // Position monde de l'origine
}
```

### Organisation mémoire

Les valeurs sont stockées linéairement dans un `DynamicBuffer<ScalarFieldItem>` :

```
Index = x + z * GridSize.x + y * GridSize.x * GridSize.z
```

### Exemple visuel (2D simplifié)

```
GridSize = (5, 5)

     0     1     2     3     4    ← x
   ┌─────┬─────┬─────┬─────┬─────┐
 0 │ +2  │ +1  │ +0.5│ +1  │ +2  │
   ├─────┼─────┼─────┼─────┼─────┤
 1 │ +1  │ -0.5│ -1  │ -0.5│ +1  │
   ├─────┼─────┼─────┼─────┼─────┤
 2 │ +0.5│ -1  │ -2  │ -1  │ +0.5│  ← Surface passe entre + et -
   ├─────┼─────┼─────┼─────┼─────┤
 3 │ +1  │ -0.5│ -1  │ -0.5│ +1  │
   ├─────┼─────┼─────┼─────┼─────┤
 4 │ +2  │ +1  │ +0.5│ +1  │ +2  │
   └─────┴─────┴─────┴─────┴─────┘
 ↑
 y
```

---

## 2. Octree

### Concept

L'octree est une **structure hiérarchique** qui représente le scalar field de manière compacte :

- **Subdivise l'espace** en 8 enfants récursivement
- **S'arrête** quand une région est uniforme (pas de surface, pas de variation)
- **Continue jusqu'à maxDepth** dans les régions avec surface potentielle

### Pourquoi un octree ?

| Sans octree | Avec octree |
|-------------|-------------|
| Traite toutes les cellules (64³ = 262,144) | Traverse uniquement les régions avec surface |
| Coût O(n³) | Coût O(surface) ≈ O(n²) |
| Pas d'adaptation | Adaptatif selon la géométrie |

### Structures de données

```csharp
// Nœud de l'octree
public struct OctreeNode : IBufferElementData
{
    public float Value;     // Valeur scalaire (moyenne ou exacte pour feuille)
    public int ChildIndex;  // Index du premier enfant (-1 si feuille)
    public int3 Position;   // Position en coordonnées grille
}

// Métadonnées de l'octree
public struct OctreeInfos : IComponentData
{
    public float3 OctreeOffset;  // Position monde (= ScalarFieldOffset)
    public int MaxDepth;         // Profondeur max (log2 de GridSize)
    public float MaxNodeSize;    // Taille du nœud racine
    public float MinNodeSize;    // Taille des feuilles (= CellSize)
    public int3 GridSize;        // Dimensions de la grille
}
```

### Structure hiérarchique

```
                    ┌───────────────────┐
         Depth 0    │    Root Node      │  Size = GridSize (ex: 32)
                    │  ChildIndex = 1   │
                    └─────────┬─────────┘
                              │
        ┌──────┬──────┬──────┼──────┬──────┬──────┬──────┐
        ▼      ▼      ▼      ▼      ▼      ▼      ▼      ▼
     ┌─────┐┌─────┐┌─────┐┌─────┐┌─────┐┌─────┐┌─────┐┌─────┐
D=1  │ C0  ││ C1  ││ C2  ││ C3  ││ C4  ││ C5  ││ C6  ││ C7  │  Size = 16
     │CI=-1││CI=9 ││CI=-1││CI=-1││CI=-1││CI=-1││CI=-1││CI=-1│
     └─────┘└──┬──┘└─────┘└─────┘└─────┘└─────┘└─────┘└─────┘
       Leaf    │                   Leaves (régions uniformes)
               │
     ┌─────────┴─────────┐
     ▼                   ▼
  ┌─────┐             ┌─────┐
  │ ... │   ...       │ ... │  Size = 8, puis 4, 2, 1...
  └─────┘             └─────┘
```

### Indexation des 8 enfants

Les 8 enfants sont stockés consécutivement à partir de `ChildIndex` :

```
Enfant  Offset binaire   Position relative
  0        000           (0, 0, 0)
  1        001           (1, 0, 0)  ← bit 0 = X
  2        010           (0, 1, 0)  ← bit 1 = Y
  3        011           (1, 1, 0)
  4        100           (0, 0, 1)  ← bit 2 = Z
  5        101           (1, 0, 1)
  6        110           (0, 1, 1)
  7        111           (1, 1, 1)
```

### Construction (OctreeSystem)

```
Pour chaque nœud à traiter:
  1. Si depth >= maxDepth:
     → Stocker la valeur exacte du scalar field
     → Marquer comme feuille (ChildIndex = -1)

  2. Sinon, analyser la région:
     → Calculer si changement de signe (hasSignChange)
     → Calculer si variation significative (hasVariation)

  3. Si pas de sign change ET pas de variation:
     → Stocker valeur moyenne
     → Marquer comme feuille (région uniforme, pas de surface)

  4. Sinon:
     → Créer 8 enfants
     → Les ajouter à la pile de traitement
```

### Requête de valeur (GetValueAtPosition)

Pour obtenir la valeur scalaire à une position grille :

```
fonction GetValueAtPosition(position):
    nœud = racine
    depth = 0

    tant que vrai:
        si nœud est feuille:
            retourner nœud.Value

        tailleNœud = 1 << (maxDepth - depth)
        demiTaille = tailleNœud / 2
        posRelative = position - nœud.Position

        // Déterminer quel enfant contient la position
        indexEnfant = 0
        si posRelative.x >= demiTaille: indexEnfant |= 1
        si posRelative.y >= demiTaille: indexEnfant |= 2
        si posRelative.z >= demiTaille: indexEnfant |= 4

        nœud = octree[nœud.ChildIndex + indexEnfant]
        depth++
```

---

## 3. Dual Contouring Cells

### Concept

Les cellules de dual contouring représentent les **cellules de la grille qui contiennent une portion de surface**. Contrairement au scalar field (qui stocke des valeurs aux points) et à l'octree (qui stocke une hiérarchie de valeurs), les cells représentent des **volumes** avec un **vertex de surface calculé**.

### Différence fondamentale

```
SCALAR FIELD          OCTREE                 DUAL CONTOURING CELL
    │                    │                          │
    ▼                    ▼                          ▼
┌───●───┐           ┌─────────┐               ┌─────────┐
│   │   │           │  Node   │               │    ◆    │ ← Vertex calculé
●───●───●           │ Value   │               │   /│\   │   (position optimale)
│   │   │           │ Position│               │  / │ \  │
└───●───┘           └─────────┘               └─────────┘
                                               8 coins vérifiés
Points de la      Représentation              Volume avec surface
grille            hiérarchique                traversante
```

### Structures de données

```csharp
// Cellule contenant un vertex de surface
public struct DualContouringCell : IBufferElementData
{
    public float3 Position;       // Position monde du coin min de la cellule
    public float Size;            // Taille de la cellule
    public bool HasVertex;        // true si surface traverse cette cellule
    public float3 VertexPosition; // Position monde du vertex (optimisé par QEF)
    public float3 Normal;         // Normale de surface au vertex
    public int3 GridIndex;        // Coordonnées grille de la cellule
}

// Intersection surface-arête (utilisé pour le calcul QEF)
public struct DualContouringEdgeIntersection : IBufferElementData
{
    public float3 Position;  // Point d'intersection sur l'arête
    public float3 Normal;    // Normale à ce point
    public int CellIndex;    // Index de la cellule
}
```

### Les 8 coins d'une cellule

Une cellule à la position grille `(x, y, z)` a 8 coins aux positions :

```
      6 ─────────── 7
     /│            /│
    / │           / │
   4 ─────────── 5  │
   │  │          │  │       Coin  Offset   Position
   │  2 ─────────│─ 3         0   (0,0,0)  (x,   y,   z  )
   │ /           │ /          1   (1,0,0)  (x+1, y,   z  )
   │/            │/           2   (0,1,0)  (x,   y+1, z  )
   0 ─────────── 1            3   (1,1,0)  (x+1, y+1, z  )
                              4   (0,0,1)  (x,   y,   z+1)
                              5   (1,0,1)  (x+1, y,   z+1)
                              6   (0,1,1)  (x,   y+1, z+1)
                              7   (1,1,1)  (x+1, y+1, z+1)
```

### Configuration de cellule

La **configuration** encode quels coins sont à l'intérieur (< 0) ou à l'extérieur (>= 0) :

```csharp
int config = 0;
for (int i = 0; i < 8; i++)
{
    float value = GetValueAtCorner(i);
    if (value >= 0)  // Extérieur
        config |= (1 << i);
}
```

| Config | Signification |
|--------|---------------|
| 0      | Tous intérieur → pas de surface |
| 255    | Tous extérieur → pas de surface |
| Autre  | Surface traverse la cellule |

### Calcul du vertex (QEF Solver)

Le vertex est placé à la **position optimale** qui minimise l'erreur quadratique par rapport aux plans tangents aux intersections :

```
1. Trouver les intersections surface/arêtes (12 arêtes possibles)
2. Pour chaque intersection:
   - Calculer la position par interpolation linéaire
   - Calculer la normale par différence finie
3. Résoudre le système QEF:
   - Minimiser Σ (distance au plan tangent)²
   - Contraindre le vertex à rester dans la cellule
```

---

## 4. Pipeline Complet

### Phase 1: Scalar Field → Octree (OctreeSystem)

```
Entrée: DynamicBuffer<ScalarFieldItem>
Sortie: DynamicBuffer<OctreeNode>

1. Créer nœud racine couvrant toute la grille
2. Subdiviser récursivement:
   - Régions uniformes → feuilles (pas de surface)
   - Régions avec variation → 8 enfants
3. À maxDepth: stocker valeurs exactes
```

### Phase 2: Octree → Cells (DualContouringOctreeSystem)

```
Entrée: DynamicBuffer<OctreeNode>
Sortie: DynamicBuffer<DualContouringCell>

1. Traverser l'octree (depth-first)
2. Pour chaque feuille à maxDepth:
   - Vérifier les 8 coins via GetValueAtPosition()
   - Si changement de signe (config ≠ 0 et ≠ 255):
     - Calculer intersections sur les arêtes
     - Résoudre QEF pour position optimale
     - Calculer normale moyenne
     - Ajouter DualContouringCell
```

### Phase 3: Cells → Mesh (DualContouringMeshGenerationSystem)

```
Entrée: DynamicBuffer<DualContouringCell>
Sortie: DynamicBuffer<DualContouringMeshVertex>
        DynamicBuffer<DualContouringMeshTriangle>

1. Construire buffer de vertices:
   - Un vertex par cellule avec HasVertex = true
   - HashMap: GridIndex → VertexIndex

2. Générer les faces (quads → 2 triangles):
   Pour chaque cellule avec vertex:
     Pour chaque axe (X, Y, Z):
       - Trouver 4 cellules adjacentes formant un quad
       - Si les 4 ont un vertex: créer 2 triangles
       - Corriger le winding selon la normale

3. Calculer bounding box
```

### Génération des faces

Les faces sont générées aux **arêtes partagées** entre 4 cellules :

```
         cellule (x,y,z)     cellule (x+1,y,z)
              ┌────────────────┬────────────────┐
              │                │                │
              │       ●        │       ●        │
              │    vertex      │    vertex      │
              │                │                │
         ─────┼────────────────┼────────────────┼─────
              │                │                │
              │       ●        │       ●        │
              │    vertex      │    vertex      │
              │                │                │
              └────────────────┴────────────────┘
         cellule (x,y+1,z)   cellule (x+1,y+1,z)

                      ↓ Génère un quad (2 triangles)

              ●───────────────●
              │ \             │
              │   \     T2    │
              │     \         │
              │  T1   \       │
              │         \     │
              ●───────────────●
```

---

## 5. Comparaison des structures

| Aspect | Scalar Field | Octree | DC Cell |
|--------|--------------|--------|---------|
| **Représente** | Valeurs aux points | Hiérarchie de valeurs | Volume avec surface |
| **Unité** | Point (0D) | Région (3D variable) | Cellule fixe (3D) |
| **Stocke** | float Value | Value + ChildIndex + Position | Vertex + Normal + GridIndex |
| **Taille mémoire** | O(n³) | O(surface) | O(surface) |
| **Accès** | Direct par index | Traversée arbre | Direct par index |
| **Responsabilité** | Données brutes | Compression spatiale | Géométrie de surface |

---

## 6. Fichiers du projet

| Fichier | Rôle |
|---------|------|
| `ScalarField/ScalarFieldItem.cs` | Structure de valeur scalaire |
| `ScalarField/ScalarFieldInfos.cs` | Métadonnées du scalar field |
| `ScalarField/ScalarFieldUtility.cs` | Conversion coordonnées ↔ index |
| `Octrees/OctreeNode.cs` | Structure de nœud octree |
| `Octrees/OctreeInfos.cs` | Métadonnées de l'octree |
| `Octrees/OctreeSystem.cs` | Construction de l'octree |
| `Octrees/OctreeUtils.cs` | Requêtes sur l'octree |
| `DualContouring/DualContouringCell.cs` | Structure de cellule DC |
| `DualContouring/DualContouringOctreeSystem.cs` | Génération des cellules |
| `DualContouring/DualContouringOctreeHelper.cs` | Calcul vertex/normale |
| `DualContouring/QefSolver.cs` | Optimisation position vertex |
| `MeshGeneration/DualContouringMeshGenerationSystem.cs` | Génération du mesh |

---

## 7. Optimisations clés

### Octree adaptatif
- Les régions uniformes ne sont pas subdivisées
- Réduit drastiquement le nombre de cellules à traiter
- Gain typique: ~90% des cellules ignorées

### Traversée efficace
- Parcours depth-first avec stack (pas de récursion)
- Compatible Burst pour vectorisation
- Allocation temporaire avec `Allocator.Temp`

### Génération de mesh sparse
- HashMap pour lookup rapide des vertices par position
- Faces générées uniquement aux frontières de surface
- Winding correction pour éviter les artefacts visuels
