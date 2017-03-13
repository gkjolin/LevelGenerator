﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class GridPosition{
	private Dictionary<Vector2,GridPosition> adjacentPositions; //Only relevant for hallway mesh
	public bool visitedByAstar = false;
	private static AStarGrid grid;
	private Vector3 doorDirection; //Only relevant for hallway mesh
	private bool isPartOfPath;
	private Vector2 direction; //hallway mesh
	private bool isAccessible;
	private int roomID;
	private int doorID;
	public float x;
	public float y;
	public int i; 
	public int j;

	public GridPosition (float x, float y){
		InitAdjacentDict ();
		isPartOfPath = false;
		doorDirection = Vector3.zero;
		direction = Vector2.zero;
		this.x = x;
		this.y = y;
		isAccessible = true;
		roomID = -1;
		doorID = -1;
	}

	private void InitAdjacentDict(){
		adjacentPositions = new Dictionary<Vector2, GridPosition> ();
		adjacentPositions.Add (Vector2.up, null);
		adjacentPositions.Add (Vector2.right, null);
		adjacentPositions.Add (Vector2.down, null);
		adjacentPositions.Add (Vector2.left, null);
	}

	public Vector2 Position {
		get{ return new Vector2 (x, y); }
		set{
			x = value.x;
			y = value.y;
		}
	}

	public bool IsAccessible {
		get {
			return this.isAccessible;
		}
		set {
			isAccessible = value;
		}
	}

	public int RoomID {
		get {
			return this.roomID;
		}
		set {
			isAccessible = value == 0;
			roomID = value;
		}
	}

	public int DoorID {
		get {
			return this.doorID;
		}
		set {
			doorID = value;
		}
	}

	public void AddAdjacent(Vector2 direction, GridPosition adjacent){
		if (direction != Vector2.zero) {
			adjacentPositions [direction] = adjacent;
		}
	}

	public Vector3 DoorDirection {
		get {
			return this.doorDirection;
		}
		set {
			doorDirection = value;
		}
	}

	public Dictionary<Vector2, GridPosition> AdjacentPositions {
		get {
			return this.adjacentPositions;
		}
	}

	public Vector2 Direction {
		get {
			if (direction == Vector2.zero || doorID > -1) {
				direction = new Vector2 (doorDirection.x, doorDirection.z);
			}
			return this.direction;
		}
		set {
			direction = value;
		}
	}

	public bool IsDoor{
		get{
			return  doorID > -1;
		}
	}

	public bool IsPartOfPath {
		get {
			return this.isPartOfPath;
		}
		set {
			isPartOfPath = value;
		}
	}

	public static AStarGrid Grid {
		get {
			return grid;
		}
		set {
			grid = value;
		}
	}
}

public class AStarGrid {

	List<RoomTransformation> rooms;
	private GridPosition[,] grid;
	private Rect availableSpace;
	private List<Rect> roomRects;
	private float gridCellSize;
	private float doorSize;

	public AStarGrid(List<Rect> roomRects, List<RoomTransformation> rooms){
		GridPosition.Grid = this;
		this.doorSize = DoorDefinition.GlobalSize;
		this.gridCellSize = DoorDefinition.GlobalSize;
		this.roomRects = roomRects;
		this.rooms = rooms;
		CalculateSpace();
		BuildGrid ();
		SortOut ();
		FindDoorNodes ();
	}

	//Iterate through all rooms and their doors and search for their corresponding nodes
	//Since door node are always inside of the room, they have to be set to accessiable again
	private void FindDoorNodes(){
		foreach (RoomTransformation room in rooms) {
			foreach (DoorDefinition door in room.Doors) {
				int x = (int)Mathf.Round((door.Position.x - availableSpace.xMin) / gridCellSize);
				int y = (int)Mathf.Round((door.Position.z - availableSpace.yMin) / gridCellSize);
				grid [x, y].DoorID = door.ID;
				grid [x, y].DoorDirection = door.Direction;
				grid [x, y].IsAccessible = true;
			}
		}
	}

	//Remove nodes that are either in a room of too far away from any room
	private void SortOut(){
		Rect[] inflatedRoomRects = InflateBy (doorSize / 2f);
		Rect[] invertedRoomRects = InflateByRoomDistance ();
		InflateBy (gridCellSize);

		//Remove GridPosition if it is within a room
		for (int i = 0; i < grid.GetLength (0); i++) {
			for (int j = 0; j < grid.GetLength (1); j++) {
				GridPosition pos = grid [i, j];
				grid [i, j].IsAccessible = !IsFree (invertedRoomRects, pos.Position);
				if (pos.IsAccessible) {
					grid [i, j].RoomID = CollidesWithRoom (inflatedRoomRects, pos.Position);
				}
			}
		}
	}

	private Rect InflateBy(Rect original, float val){
		Rect inflated = original;
		inflated.yMin -= val;
		inflated.xMin -= val;
		inflated.xMax += val;
		inflated.yMax += val;
		return inflated;
	}

	private Rect[] InflateBy(float val){
		List<Rect> inflatedRects = new List<Rect> ();
		for(int i = 0; i < roomRects.Count; i++) {			
			Rect r = InflateBy (rooms [i].Rect, val);
			inflatedRects.Add (r);
		}
		return inflatedRects.ToArray ();
	}

	private Rect[] InflateByRoomDistance(){
		List<Rect> inflatedRects = new List<Rect> ();
		for(int i = 0; i < roomRects.Count; i++) {			
			Rect r = InflateBy (rooms [i].Rect, rooms[i].FurthestDistance / 2f);
			inflatedRects.Add (r);
		}
		return inflatedRects.ToArray ();
	}

	private void BuildGrid(){
		gridCellSize = 2f;
		int xIterations = (int)(availableSpace.width / gridCellSize);
		int yIterations = (int)(availableSpace.height / gridCellSize);

		grid = new GridPosition[xIterations, yIterations];	

		for (int i = 0; i < xIterations; i++) {
			for (int j = 0; j < yIterations; j++) {
				Vector2 newPos = new Vector2 (availableSpace.xMin + i * gridCellSize, availableSpace.yMin + j * gridCellSize);
				grid [i, j] = new GridPosition (newPos.x, newPos.y);
				grid [i, j].i = i;
				grid [i, j].j = j;
			}
		}
	}

	//Calculates a rectangle that contains all rooms
	//Used to build and optimize the grid
	private void CalculateSpace(){
		availableSpace = new Rect (Vector2.zero, Vector2.zero);

		foreach (Rect room in roomRects) {
			availableSpace.xMin = Mathf.Min (availableSpace.xMin, room.xMin);
			availableSpace.yMin = Mathf.Min (availableSpace.yMin, room.yMin);
			availableSpace.xMax = Mathf.Max (availableSpace.xMax, room.xMax);
			availableSpace.yMax = Mathf.Max (availableSpace.yMax, room.yMax);
		}

		availableSpace.yMin -= doorSize * 4f;
		availableSpace.xMin -= doorSize * 4f;
		availableSpace.xMax += doorSize * 4f;
		availableSpace.yMax += doorSize * 4f;
	}

	//Returns true, if the position is within a room rect
	//Returns a room id
	private int CollidesWithRoom(Rect[] rects, Vector2 position){
		int id = 0;
		foreach (Rect rect in rects) {
			id++;
			if (rect.Contains (position)) {
				return id;
			}
		}
		return 0;
	}

	//Returns true, if the given position is not colliding with any of the given rects
	private bool IsFree(Rect[] rects, Vector2 position){
		return CollidesWithRoom (rects, position) == 0;
	}

	public GridPosition[,] Grid {
		get {
			return this.grid;
		}
	}

	//Returns a new instance of a Square (AStar Grid Element) at the given Vector2 position
	//Used for getting the squares for the start and end door, of which at first only the positions are known
	public Square SquareAtCoordinate(Vector2 position){
		int[] pos = new int[2];
		pos[0] = (int)Mathf.Round((position.x - availableSpace.xMin) / gridCellSize);
		pos[1] = (int)Mathf.Round((position.y - availableSpace.yMin) / gridCellSize);
		Square square = new Square (position, pos);
		return square;
	}

	//Returns a new Square instance from a grid position
	public Square GetSquareInGrid(int i, int j){
		//Return null, if the index is out of bounds or the element is set to be unaccessable
		if (i < 0 || i > grid.GetLength (0) - 1 || j < 0 || j > grid.GetLength (1) - 1) {
			return null;
		}
		if (!grid [i, j].IsAccessible) {
			return null;
		}
		grid [i, j].visitedByAstar = true;
		return new Square (grid [i, j].Position, new int[]{ i, j });
	}

	//Removes the Y-Coordinate from a Vec3 and returns the resulting Vec2
	private Vector2 ClipY(Vector3 vec){
		return new Vector2(vec.x, vec.z);
	}

	public void MarkPositionAsUsed(Square square){
		grid [square.GridX, square.GridY].IsPartOfPath = true;
	}

	//Add adjacent relations between Square one and two
	//Used in the HallwayMeshGenerator to decide wheter to draw walls or not
	public void AddAdjacentRelation(Square one, Square two){
		GridPosition onePos = grid [one.GridX, one.GridY];
		GridPosition twoPos = grid [two.GridX, two.GridY];
		onePos.AddAdjacent (AdjacentDirection(one, two), twoPos);
		twoPos.AddAdjacent (AdjacentDirection(two, one), onePos);
	}

	//Returns the direction of Square two as seen from Square one in the grid as a Vec2
	private Vector2 AdjacentDirection(Square one, Square two){
		if (one.GridY < two.GridY) {
			return Vector2.up;
		} else if (one.GridY > two.GridY) {
			return Vector2.down;
		} else if (one.GridX < two.GridX) {
			return Vector2.right;
		} else if (one.GridX > two.GridX) {
			return Vector2.left;
		}
		return Vector2.zero;
	}

	public void UpdateDirection(Square one){
		grid [one.GridX, one.GridY].Direction = one.Direction;
	}
}