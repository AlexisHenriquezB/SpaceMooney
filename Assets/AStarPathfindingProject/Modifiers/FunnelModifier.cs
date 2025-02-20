//#define DEBUG
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Pathfinding;

[AddComponentMenu("Pathfinding/Modifiers/Funnel")]
[System.Serializable]
/** Simplifies paths on navmesh graphs using the funnel algorithm.
 * The funnel algorithm is an algorithm which can, given a path corridor with nodes in the path where the nodes have an area, like triangles, it can find the shortest path inside it.
 * This makes paths on navmeshes look much cleaner and smoother.
 * \image html images/funnelModifier_on.png
 * \ingroup modifiers
 */
public class FunnelModifier : MonoModifier
{

#if UNITY_EDITOR
	[UnityEditor.MenuItem ("CONTEXT/Seeker/Add Funnel Modifier")]
	public static void AddComp (UnityEditor.MenuCommand command) {
		(command.context as Component).gameObject.AddComponent (typeof(FunnelModifier));
	}
#endif

	/* Get start and end points from \link Path::vectorPath vectorPath \endlink. Set this to true if you are using the Start End modifier and want to have more accurate start and end points (the ones you passed to StartPath) */
	//public bool useVectorStartEnd = true;

	//Used to reduce allocations
	private static List<Vector3> tmpList;
	private static Int3[] leftFunnel;
	private static Int3[] rightFunnel;

	public static Vector3[] lastLeftFunnel;
	public static Vector3[] lastRightFunnel;

	public override ModifierData input
	{
		get { return ModifierData.StrictVectorPath; }
	}

	public override ModifierData output
	{
		get { return ModifierData.VectorPath; }
	}

	[System.Obsolete("Don't use this, probably doesn't work")]
	public static new Vector3 GetNextTarget(Path path, Vector3 currPosition)
	{
		return GetNextTarget(path, currPosition, 0, 0);
	}

	[System.Obsolete("Don't use this, probably doesn't work")]
	public static Vector3 GetNextTarget(Path path, Vector3 currPosition, float offset, float pickNextWaypointMargin)
	{

		if (path.error)
		{
			return currPosition;
		}

		Int3 currentPosition = (Int3)currPosition;

		int startIndex = 0;
		int endIndex = path.path.Length;

		//Int3 pos = (Int3)currentPosition;

		//int minDist = -1;
		//int minNode = -1;

		INavmesh navmeshGraph = AstarData.GetGraph(path.path[0]) as INavmesh;

		if (navmeshGraph == null)
		{
			Debug.LogError("Couldn't cast graph to the appropriate type (graph isn't a Navmesh type graph, it doesn't implement the INavmesh interface)");
			return currPosition;///Apply (path,start,end, startIndex, endIndex, graph);
		}

		Int3[] vertices = navmeshGraph.vertices;

		//float minDist2 = -1;
		//int minNode2 = -1;

		//Debug.Log (meshPath.Length + " "+path.Length +" "+startIndex+" "+endIndex);
		/*for (int i=startIndex;i< endIndex;i++) {
			MeshNode node = path.path[i] as MeshNode;
			
			if (node == null) {
				Debug.LogWarning ("Path could not be casted to Mesh Nodes");
				return currentPosition;//return base.Apply (path,start,end, startIndex, endIndex, graph);
			}
			
			//if (Polygon.TriangleArea2 (vertices[node.v1],vertices[node.v2],currentPosition) >= 0 || Polygon.TriangleArea2 (vertices[node.v2],vertices[node.v3],currentPosition) >= 0 || Polygon.TriangleArea2 (vertices[node.v3],vertices[node.v1],currentPosition) >= 0) {
				
			if (!Polygon.IsClockwise (vertices[node.v1],vertices[node.v2],pos) || !Polygon.IsClockwise (vertices[node.v2],vertices[node.v3],pos) || !Polygon.IsClockwise (vertices[node.v3],vertices[node.v1],pos)) {
				
				if (minDist == -1) {
					float dist2 = (node.position-pos).sqrMagnitude;
					if (minDist2 == -1 || dist2 < minDist2) {
						minDist2 = dist2;
						minNode2 = i;
					}
				}
				continue;
			}
			
			Debug.DrawLine (vertices[node.v1],vertices[node.v2],Color.blue);
			Debug.DrawLine (vertices[node.v2],vertices[node.v3],Color.blue);
			Debug.DrawLine (vertices[node.v3],vertices[node.v1],Color.blue);
			
			
			int dist = node.position.y-pos.y;
			
			if (minDist == -1 || dist < minDist) {
				minDist = dist;
				minNode = i;
			}
			
		}*/

		MeshNode lastNode = path.path[endIndex - 1] as MeshNode;
		if (lastNode == null)
		{
			Debug.LogWarning("Path could not be casted to Mesh Nodes");
			return currentPosition;//return base.Apply (path,start,end, startIndex, endIndex, graph);
		}

		PathNNConstraint constraint = PathNNConstraint.Default;
		constraint.SetStart(lastNode);
		NNInfo nninfo = NavMeshGraph.GetNearestForce(path.path, vertices, currPosition, constraint);

		currentPosition = nninfo.clampedPosition;
		/*for (int i=startIndex;i< endIndex;i++) {
			if (nninfo.node == path.path[i]) {
				minNode = i;
			}
		}*/

		//Node minNode = nninfo.node;

		/*startIndex = minNode;
		if (startIndex == -1) {
			startIndex = minNode2;
			currentPosition = path.path[minNode2].position;
		}
		if (startIndex == -1) {
			Debug.Log ("Couldn't find current node");
			return currentPosition;
		}*/

		MeshNode[] meshPath = new MeshNode[endIndex - startIndex];

		for (int i = startIndex; i < endIndex; i++)
		{
			meshPath[i - startIndex] = path.path[i] as MeshNode;
		}
		//return Vector3.zero;

		//Vector3[] vertices = null;

		if (leftFunnel == null || leftFunnel.Length < meshPath.Length + 1)
		{
			leftFunnel = new Int3[meshPath.Length + 1];
		}
		if (rightFunnel == null || rightFunnel.Length < meshPath.Length + 1)
		{
			rightFunnel = new Int3[meshPath.Length + 1];
		}
		int leftFunnelLength = meshPath.Length + 1;
		//int rightFunnelLength = meshPath.Length+1;

		leftFunnel[0] = currentPosition;
		rightFunnel[0] = currentPosition;

		leftFunnel[meshPath.Length] = path.endPoint;
		rightFunnel[meshPath.Length] = path.endPoint;

		int lastLeftIndex = -1;
		int lastRightIndex = -1;

		for (int i = 0; i < meshPath.Length - 1; i++)
		{
			//Find the connection between the nodes

			MeshNode n1 = meshPath[i];
			MeshNode n2 = meshPath[i + 1];

			bool foundFirst = false;

			int first = -1;
			int second = -1;

			for (int x = 0; x < 3; x++)
			{
				//Vector3 vertice1 = vertices[n1.vertices[x]];
				//n1[x] gets the vertice index for vertex number 'x' in the node. Equal to n1.GetVertexIndex (x)
				int vertice1 = n1[x];
				for (int y = 0; y < 3; y++)
				{
					//Vector3 vertice2 = vertices[n2.vertices[y]];
					int vertice2 = n2[y];

					if (vertice1 == vertice2)
					{
						if (foundFirst)
						{
							second = vertice2;
							break;
						}
						else
						{
							first = vertice2;
							foundFirst = true;
						}
					}
				}
			}

			//Debug.DrawLine ((Vector3)vertices[first]+Vector3.up*0.1F,(Vector3)vertices[second]+Vector3.up*0.1F,Color.cyan);
			//Debug.Log (first+" "+second);
			if (first == lastLeftIndex)
			{
				leftFunnel[i + 1] = vertices[first];
				rightFunnel[i + 1] = vertices[second];
				lastLeftIndex = first;
				lastRightIndex = second;

			}
			else if (first == lastRightIndex)
			{
				leftFunnel[i + 1] = vertices[second];
				rightFunnel[i + 1] = vertices[first];
				lastLeftIndex = second;
				lastRightIndex = first;

			}
			else if (second == lastLeftIndex)
			{
				leftFunnel[i + 1] = vertices[second];
				rightFunnel[i + 1] = vertices[first];
				lastLeftIndex = second;
				lastRightIndex = first;

			}
			else
			{
				leftFunnel[i + 1] = vertices[first];
				rightFunnel[i + 1] = vertices[second];
				lastLeftIndex = first;
				lastRightIndex = second;
			}
		}

		//Switch the arrays so the right funnel really is on the right side (and vice versa)
		if (!Polygon.IsClockwise(currentPosition, leftFunnel[1], rightFunnel[1]))
		{
			Int3[] tmp = leftFunnel;
			leftFunnel = rightFunnel;
			rightFunnel = tmp;
		}

		for (int i = 1; i < leftFunnelLength - 1; i++)
		{

			//float unitWidth = 2;
			Int3 normal = (rightFunnel[i] - leftFunnel[i]);

			float magn = normal.worldMagnitude;
			normal /= magn;
			normal *= Mathf.Clamp(offset, 0, (magn / 2F));
			leftFunnel[i] += normal;
			rightFunnel[i] -= normal;
			//Debug.DrawLine (rightFunnel[i],leftFunnel[i],Color.blue);
		}

		/*for (int i=0;i<path.Length-1;i++) {
			Debug.DrawLine (path[i].position,path[i+1].position,Color.blue);
		}*/

		/*for (int i=0;i<leftFunnel.Length-1;i++) {
			Debug.DrawLine (leftFunnel[i],leftFunnel[i+1],Color.red);
			Debug.DrawLine (rightFunnel[i],rightFunnel[i+1],Color.magenta);
		}*/

		if (tmpList == null)
		{
			tmpList = new List<Vector3>(3);
		}

		List<Vector3> funnelPath = tmpList;
		funnelPath.Clear();

		funnelPath.Add(currentPosition);

		Int3 portalApex = currentPosition;
		Int3 portalLeft = leftFunnel[0];
		Int3 portalRight = rightFunnel[0];

		int apexIndex = 0;
		int rightIndex = 0;
		int leftIndex = 0;

		//yield return 0;

		for (int i = 1; i < leftFunnelLength; i++)
		{

			Int3 left = leftFunnel[i];
			Int3 right = rightFunnel[i];

			/*Debug.DrawLine (portalApex,portalLeft,Color.red);
			Debug.DrawLine (portalApex,portalRight,Color.yellow);
			Debug.DrawLine (portalApex,left,Color.cyan);
			Debug.DrawLine (portalApex,right,Color.cyan);*/

			if (Polygon.TriangleArea2(portalApex, portalRight, right) >= 0)
			{

				if (portalApex == portalRight || Polygon.TriangleArea2(portalApex, portalLeft, right) <= 0)
				{
					portalRight = right;
					rightIndex = i;
				}
				else
				{
					funnelPath.Add((Vector3)portalLeft);

					//if (funnelPath.Count > 3)
					//break;

					portalApex = portalLeft;
					apexIndex = leftIndex;

					portalLeft = portalApex;
					portalRight = portalApex;

					leftIndex = apexIndex;
					rightIndex = apexIndex;

					i = apexIndex;

					//yield return 0;
					continue;
				}
			}

			if (Polygon.TriangleArea2(portalApex, portalLeft, left) <= 0)
			{

				if (portalApex == portalLeft || Polygon.TriangleArea2(portalApex, portalRight, left) >= 0)
				{
					portalLeft = left;
					leftIndex = i;

				}
				else
				{

					funnelPath.Add((Vector3)portalRight);

					//if (funnelPath.Count > 3)
					//break;

					portalApex = portalRight;
					apexIndex = rightIndex;

					portalLeft = portalApex;
					portalRight = portalApex;

					leftIndex = apexIndex;
					rightIndex = apexIndex;

					i = apexIndex;

					//yield return 0;
					continue;
				}
			}

			//yield return 0;
		}

		//yield return 0;

		funnelPath.Add(path.endPoint);

		Vector3[] p = funnelPath.ToArray();


		if (p.Length <= 1)
		{
			return currentPosition;
		}

		for (int i = 1; i < p.Length - 1; i++)
		{
			Vector3 closest = Mathfx.NearestPointStrict(p[i], p[i + 1], currentPosition);
			if ((closest - (Vector3)currentPosition).sqrMagnitude < pickNextWaypointMargin * pickNextWaypointMargin)
			{
				continue;
			}
			else
			{
				return p[i];
			}
		}
		return p[p.Length - 1];
	}

	public override void Apply(Path p, ModifierData source)
	{
		Node[] path = p.path;
		Vector3[] vectorPath = p.vectorPath;

		if (path == null || path.Length == 0 || vectorPath == null || vectorPath.Length != path.Length)
		{
			return;
		}

		//The graph index for the current nodes
		int currentGraphIndex = path[0].graphIndex;

		//First node which is in the graph currentGraphIndex
		int currentGraphStart = 0;

		List<Vector3> funnelPath = new List<Vector3>();

		List<Vector3> left = new List<Vector3>();
		List<Vector3> right = new List<Vector3>();

		for (int i = 0; i < path.Length; i++)
		{

			if (path[i].graphIndex != currentGraphIndex)
			{
				IFunnelGraph funnelGraph = AstarData.GetGraph(path[currentGraphStart]) as IFunnelGraph;

				if (funnelGraph == null)
				{
					Debug.Log("Funnel Graph is null");
					for (int j = currentGraphStart; j <= i; j++)
					{
						funnelPath.Add(path[j].position);
					}
				}
				else
				{
					ConstructFunnel(funnelGraph, vectorPath, path, currentGraphStart, i, funnelPath, left, right);
				}


				currentGraphIndex = path[i].graphIndex;
				currentGraphStart = i;
			}
		}

		IFunnelGraph funnelGraph2 = AstarData.GetGraph(path[currentGraphStart]) as IFunnelGraph;

		if (funnelGraph2 == null)
		{
			for (int j = currentGraphStart; j < path.Length - 1; j++)
			{
				funnelPath.Add(path[j].position);
			}
		}
		else
		{
			ConstructFunnel(funnelGraph2, vectorPath, path, currentGraphStart, path.Length - 1, funnelPath, left, right);
		}

		p.vectorPath = funnelPath.ToArray();

	}

	public void ConstructFunnel(IFunnelGraph funnelGraph, Vector3[] vectorPath, Node[] path, int sIndex, int eIndex, List<Vector3> funnelPath, List<Vector3> left, List<Vector3> right)
	{
		//Construct a funnel corridor for the nodes in the path segment
		left.Clear();
		right.Clear();

		left.Add(vectorPath[sIndex]);
		right.Add(vectorPath[sIndex]);

		funnelGraph.BuildFunnelCorridor(path, sIndex, eIndex, left, right);

		left.Add(vectorPath[eIndex]);
		right.Add(vectorPath[eIndex]);

		if (!RunFunnel(left, right, funnelPath))
		{

			//Add the start and end positions to the path
			funnelPath.Add(vectorPath[sIndex]);
			funnelPath.Add(vectorPath[eIndex]);

			//If the funnel failed (probably too short), add the individual positions instead
			//for (int j=sIndex;j<=eIndex;j++) {
			//	funnelPath.Add (path[j].position);
			//}
		}

		lastLeftFunnel = left.ToArray();
		lastRightFunnel = right.ToArray();
	}

	public bool RunFunnel(List<Vector3> left, List<Vector3> right, List<Vector3> funnelPath)
	{

		if (left.Count <= 3)
		{
			return false;
		}

		//System.Console.WriteLine ("Start");

		//Remove identical vertices
		while (left[1] == left[2] && right[1] == right[2])
		{
			//System.Console.WriteLine ("Removing identical left and right");
			left.RemoveAt(1);
			right.RemoveAt(1);

			if (left.Count <= 3)
			{
				return false;
			}

		}

		/*while (right[1] == right[2]) {
			System.Console.WriteLine ("Removing identical right");
			right.RemoveAt (1);
			left.RemoveAt (1);
		}*/

		Vector3 swPoint = left[2];
		if (swPoint == left[1])
		{
			swPoint = right[2];
		}

		/*if (Polygon.IsColinear (left[0],left[1],right[1])) {
			System.Console.WriteLine ("	Colinear");
			left[0] += (left[2]-left[0]).normalized*0.001F;
			if (Polygon.IsColinear (left[0],left[1],right[1])) {
				Debug.LogError ("WUT!!!");//NOTE - CAN ACTUALLY HAPPEN!
			}
		}*/


		//Solves cases where the start point lies on the wrong side of the first funnel portal
		/*if (Polygon.IsColinear (left[0],left[1],right[1]) || Polygon.Left (left[1],right[1],swPoint) == Polygon.Left (left[1],right[1],left[0])) {
			Debug.DrawLine (left[1],right[1],new Color (0,0,0,0.5F));
			Debug.DrawLine (left[0],swPoint,new Color (0,0,0,0.5F));
			System.Console.WriteLine ("Wrong Side");
			left[0] = Mathfx.NearestPointStrict (left[1],right[1],left[0]);
			left[0] += (left[0]-swPoint).normalized*0.001F;//Tiny move to the right side to prevent floating point errors, too bad with that .normalized call though, could perhaps be optimized
			right[0] = left[0];
		}*/

		//Test
		while (Polygon.IsColinear(left[0], left[1], right[1]) || Polygon.Left(left[1], right[1], swPoint) == Polygon.Left(left[1], right[1], left[0]))
		{

			Debug.DrawLine(left[1], right[1], new Color(0, 0, 0, 0.5F));
			Debug.DrawLine(left[0], swPoint, new Color(0, 0, 0, 0.5F));

			left.RemoveAt(1);
			right.RemoveAt(1);

			if (left.Count <= 3)
			{
				return false;
			}

			/*System.Console.WriteLine ("Wrong Side");
			left[0] = Mathfx.NearestPointStrict (left[1],right[1],left[0]);
			left[0] += (left[0]-swPoint).normalized*0.001F;//Tiny move to the right side to prevent floating point errors, too bad with that .normalized call though, could perhaps be optimized
			right[0] = left[0];*/

			swPoint = left[2];
			if (swPoint == left[1])
			{
				swPoint = right[2];
			}
		}

		//Switch left and right to really be on the "left" and "right" sides
		if (!Polygon.IsClockwise(left[0], left[1], right[1]) && !Polygon.IsColinear(left[0], left[1], right[1]))
		{
			//System.Console.WriteLine ("Wrong Side 2");
			List<Vector3> tmp = left;
			left = right;
			right = tmp;
		}

		/*for (int i=1;i<leftFunnel.Length-1;i++) {
			
			float unitWidth = 5;
			Int3 normal = (rightFunnel[i]-leftFunnel[i]);
			float magn = normal.worldMagnitude;
			normal /= magn;
			normal *= Mathf.Clamp (unitWidth,0,(magn/2F));
			leftFunnel[i] += normal;
			rightFunnel[i] -= normal;
		}*/


		funnelPath.Add(left[0]);

		Vector3 portalApex = left[0];
		Vector3 portalLeft = left[1];
		Vector3 portalRight = right[1];

		int apexIndex = 0;
		int rightIndex = 1;
		int leftIndex = 1;

		//yield return 0;

		for (int i = 2; i < left.Count; i++)
		{

			if (funnelPath.Count > 200)
			{
				Debug.LogWarning("Avoiding infinite loop");
				break;
			}

			Vector3 pLeft = left[i];
			Vector3 pRight = right[i];

			/*Debug.DrawLine (portalApex,portalLeft,Color.red);
			Debug.DrawLine (portalApex,portalRight,Color.yellow);
			Debug.DrawLine (portalApex,left,Color.cyan);
			Debug.DrawLine (portalApex,right,Color.cyan);*/

			if (Polygon.TriangleArea2(portalApex, portalRight, pRight) >= 0)
			{

				if (portalApex == portalRight || Polygon.TriangleArea2(portalApex, portalLeft, pRight) <= 0)
				{
					portalRight = pRight;
					rightIndex = i;
				}
				else
				{
					funnelPath.Add(portalLeft);
					portalApex = portalLeft;
					apexIndex = leftIndex;

					portalLeft = portalApex;
					portalRight = portalApex;

					leftIndex = apexIndex;
					rightIndex = apexIndex;

					i = apexIndex;

					//yield return 0;
					continue;
				}
			}

			if (Polygon.TriangleArea2(portalApex, portalLeft, pLeft) <= 0)
			{

				if (portalApex == portalLeft || Polygon.TriangleArea2(portalApex, portalRight, pLeft) >= 0)
				{
					portalLeft = pLeft;
					leftIndex = i;

				}
				else
				{

					funnelPath.Add(portalRight);
					portalApex = portalRight;
					apexIndex = rightIndex;

					portalLeft = portalApex;
					portalRight = portalApex;

					leftIndex = apexIndex;
					rightIndex = apexIndex;

					i = apexIndex;

					//yield return 0;
					continue;
				}
			}

			//yield return 0;
		}

		//yield return 0;

		funnelPath.Add(left[left.Count - 1]);
		return true;
	}

	/*[System.Obsolete ("Don't use this, probably doesn't work")]
	public void Applyx (Path p, ModifierData source) {
		
		Node[] path = p.path;
		
		if (path == null || path.Length == 0) {
			return;// new Vector3[0];
		}
		
		int currentGraphIndex = path[0].graphIndex;
			
		int firstGraphIndex = 0;
		
		List<Vector3> tmpVectorPath = new List<Vector3> ();
		
		for (int i=1;i<path.Length;i++) {
			
			if (path[i].graphIndex != currentGraphIndex) {
				
				Vector3 modStart;// = firstGraphIndex == 0 ? p.startPoint : (Vector3)path[firstGraphIndex].position;
				
				if (useVectorStartEnd && firstGraphIndex == 0 && p.vectorPath != null && p.vectorPath.Length != 0) {
					modStart = p.vectorPath[0];
				} else {
					modStart = (Vector3)path[firstGraphIndex].position;
				}
		
				INavmesh graph = AstarPath.active.graphs[currentGraphIndex] as INavmesh;
				
				if (graph == null) {
					for (int q=firstGraphIndex;q<i;q++) {
						tmpVectorPath.Add (path[i].position);
					}
					continue;
				}
				
				tmpVectorPath.AddRange ( Apply (path,modStart,path[i-1].position, firstGraphIndex,i,graph as NavGraph) );
				
				//AstarPath.active.graphs[currentGraphIndex].ApplySourceModifier (path,modStart, path[i-1].position, firstGraphIndex,i)
				//tmpVectorPath.AddRange ();
				
				currentGraphIndex = path[i].graphIndex;
				firstGraphIndex = i;
			}
		}
		
		Vector3 start;
		if (useVectorStartEnd && firstGraphIndex == 0 && p.vectorPath != null && p.vectorPath.Length != 0) {
			start = p.vectorPath[0];
		} else {
			start = (Vector3)path[firstGraphIndex].position;
		}
		
		Vector3 end;// = p.endPoint;
		if (useVectorStartEnd && p.vectorPath != null && p.vectorPath.Length != 0) {
			end = p.vectorPath[p.vectorPath.Length-1];
		} else {
			end = (Vector3)path[path.Length-1].position;
		}
		INavmesh graph2 = AstarPath.active.graphs[currentGraphIndex] as INavmesh;
		
		tmpVectorPath.AddRange (Apply (path,start,end, firstGraphIndex,path.Length, graph2 as NavGraph) );
		
		p.vectorPath = tmpVectorPath.ToArray ();
		//AstarPath.active.graphs[currentGraphIndex].ApplySourceModifier (path,start, end, firstGraphIndex,count));
	}
	
	public override Vector3[] Apply (Node[] path, Vector3 startVector, Vector3 endVector, int startIndex, int endIndex, NavGraph graph) {
		
		if (endIndex-startIndex == 0) {
			return new Vector3[0];
		}
		
		Int3 start = (Int3)startVector;
		Int3 end = (Int3)endVector;
		
		MeshNode[] meshPath = new MeshNode[endIndex-startIndex];
		
		//Debug.Log (meshPath.Length + " "+path.Length +" "+startIndex+" "+endIndex);
		for (int i=startIndex;i< endIndex;i++) {
			meshPath[i-startIndex] = path[i] as MeshNode;
			
			if (meshPath[i-startIndex] == null) {
				Debug.LogError ("Path can not be casted to Mesh Nodes");
				return base.Apply (path,start,end, startIndex, endIndex, graph);
			}
		}
		
		INavmesh navmeshGraph = graph as INavmesh;
		
		if (navmeshGraph == null) {
			Debug.LogError ("Couldn't cast graph to the appropriate type (graph isn't a Navmesh type graph, it doesn't implement the INavmesh interface)");
			return base.Apply (path,start,end, startIndex, endIndex, graph);
		}
		
		Int3[] vertices = navmeshGraph.vertices;
		
		Int3[] leftFunnel = new Int3[meshPath.Length+1];
		Int3[] rightFunnel = new Int3[meshPath.Length+1];
		
		leftFunnel[0] = start;
		rightFunnel[0] = start;
		
		leftFunnel[meshPath.Length] = end;
		rightFunnel[meshPath.Length] = end;
		
		int lastLeftIndex = -1;
		int lastRightIndex = -1;
		
		for (int i=0;i<meshPath.Length-1;i++) {
			//Find the connection between the nodes
			
			MeshNode n1 = meshPath[i];
			MeshNode n2 = meshPath[i+1];
			
			bool foundFirst = false;
			
			int first = -1;
			int second = -1;
			
			for (int x=0;x<3;x++) {
				//Vector3 vertice1 = vertices[n1.vertices[x]];
				int vertice1 = n1.GetVertexIndex (x);
				for (int y=0;y<3;y++) {
					//Vector3 vertice2 = vertices[n2.vertices[y]];
					int vertice2 = n2.GetVertexIndex (y);
					
					if (vertice1 == vertice2) {
						if (foundFirst) {
							second = vertice2;
							break;
						} else {
							first = vertice2;
							foundFirst = true;
						}
					}
				}
			}
			
			if (first == -1 || second == -1) {
				leftFunnel[i+1] = n1.position;
				rightFunnel[i+1] = n1.position;
				lastLeftIndex = first;
				lastRightIndex = second;
				
			} else
			
			//Debug.DrawLine ((Vector3)vertices[first]+Vector3.up*0.1F,(Vector3)vertices[second]+Vector3.up*0.1F,Color.cyan);
			//Debug.Log (first+" "+second);
			if (first == lastLeftIndex) {
				leftFunnel[i+1] = vertices[first];
				rightFunnel[i+1] = vertices[second];
				lastLeftIndex = first;
				lastRightIndex = second;
				
			} else if (first == lastRightIndex) {
				leftFunnel[i+1] = vertices[second];
				rightFunnel[i+1] = vertices[first];
				lastLeftIndex = second;
				lastRightIndex = first;
				
			} else if (second == lastLeftIndex) {
				leftFunnel[i+1] = vertices[second];
				rightFunnel[i+1] = vertices[first];
				lastLeftIndex = second;
				lastRightIndex = first;
				
			} else {
				leftFunnel[i+1] = vertices[first];
				rightFunnel[i+1] = vertices[second];
				lastLeftIndex = first;
				lastRightIndex = second;
			}
		}
		
		//Switch the arrays so the right funnel really is on the right side (and vice versa)
		if (!Polygon.IsClockwise (start,leftFunnel[1],rightFunnel[1])) {
			Int3[] tmp = leftFunnel;
			leftFunnel = rightFunnel;
			rightFunnel = tmp;
		}
		
		/*for (int i=1;i<leftFunnel.Length-1;i++) {
			
			float unitWidth = 5;
			Int3 normal = (rightFunnel[i]-leftFunnel[i]);
			float magn = normal.worldMagnitude;
			normal /= magn;
			normal *= Mathf.Clamp (unitWidth,0,(magn/2F));
			leftFunnel[i] += normal;
			rightFunnel[i] -= normal;
		}*/
	/*for (int i=0;i<path.Length-1;i++) {
		Debug.DrawLine (path[i].position,path[i+1].position,Color.blue);
	}*
		
	#if DEBUGGING
	for (int i=0;i<leftFunnel.Length-1;i++) {
		Debug.DrawLine (leftFunnel[i],leftFunnel[i+1],Color.red);
		Debug.DrawLine (rightFunnel[i],rightFunnel[i+1],Color.magenta);
	}
	#endif
		
	List<Vector3> funnelPath = new List<Vector3> (3);
		
	funnelPath.Add (start);
		
	Vector3 portalApex = start;
	Vector3 portalLeft = leftFunnel[0];
	Vector3 portalRight = rightFunnel[0];
		
	int apexIndex = 0;
	int rightIndex = 0;
	int leftIndex = 0;
		
	//yield return 0;
		
	for (int i=1;i<leftFunnel.Length;i++) {
			
		if (funnelPath.Count > 200) {
			Debug.LogWarning ("Avoiding infinite loop");
			break;
		}
			
		Vector3 left = leftFunnel[i];
		Vector3 right = rightFunnel[i];
			
		/*Debug.DrawLine (portalApex,portalLeft,Color.red);
		Debug.DrawLine (portalApex,portalRight,Color.yellow);
		Debug.DrawLine (portalApex,left,Color.cyan);
		Debug.DrawLine (portalApex,right,Color.cyan);*
			
		if (Polygon.TriangleArea2 (portalApex,portalRight,right) >= 0) {
				
			if (portalApex == portalRight || Polygon.TriangleArea2 (portalApex,portalLeft,right) <= 0) {
				portalRight = right;
				rightIndex = i;
			} else {
				funnelPath.Add (portalLeft);
				portalApex = portalLeft;
				apexIndex = leftIndex;
					
				portalLeft = portalApex;
				portalRight = portalApex;
					
				leftIndex = apexIndex;
				rightIndex = apexIndex;
					
				i = apexIndex;
					
				//yield return 0;
				continue;
			}
		}
			
		if (Polygon.TriangleArea2 (portalApex,portalLeft,left) <= 0) {
				
			if (portalApex == portalLeft || Polygon.TriangleArea2 (portalApex,portalRight,left) >= 0) {
				portalLeft = left;
				leftIndex = i;
					
			} else {
					
				funnelPath.Add (portalRight);
				portalApex = portalRight;
				apexIndex = rightIndex;
					
				portalLeft = portalApex;
				portalRight = portalApex;
					
				leftIndex = apexIndex;
				rightIndex = apexIndex;
					
				i = apexIndex;
					
				//yield return 0;
				continue;
			}
		}
			
		//yield return 0;
	}
		
	//yield return 0;
		
	funnelPath.Add (end);
		
	Vector3[] p = funnelPath.ToArray ();
		
	/*for (int i=0;i< p.Length-1;i++) {
		Debug.DrawLine (p[i]+Vector3.up*0.2F,p[i+1]+Vector3.up*0.2F,Color.yellow);
	}*
		
		
	return p;
}*/

}

/** Graphs implementing this interface have support for the Funnel modifier */
public interface IFunnelGraph
{

	void BuildFunnelCorridor(Node[] path, int sIndex, int eIndex, List<Vector3> left, List<Vector3> right);

	/** Add the portal between node \a n1 and \a n2 to the funnel corridor. The left and right edges does not necesarily need to be the left and right edges (right can be left), they will be swapped if that is detected. But that works only as long as the edges do not switch between left and right in the middle of the path.
	  */
	void AddPortal(Node n1, Node n2, List<Vector3> left, List<Vector3> right);
}