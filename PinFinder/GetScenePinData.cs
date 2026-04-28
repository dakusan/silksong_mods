using UnityEngine;
using GlobalEnums;
using SilkDev;
using System.Linq;

namespace PinFinder;

//Getting the pin data from an object in a scene
public class GetScenePinData
{
	//Cache map data
	public GetScenePinData(string SceneName)
	{
		this.SceneName=SceneName;

		//Get other data needed to determine map positions
		GetSceneInfo(
			SceneName,
			GetMapZoneFromSceneName(SceneName),
			out GameMapScene, out SceneGameObject, out ScenePos
		);
		tk2dTileMap Tilemap=GameManager.instance.tilemap;
		TileMapSize=new Vector2(Tilemap.width, Tilemap.height);
	}
	static GetScenePinData()
	{
		//Game map
		GameMapT=GameObject.Find("Game_Map_Hornet(Clone)")?.transform ?? throw new MissingComponentException("Cannot find the primary map object");
		GameMapGM=GameMapT.gameObject.GetComponent<GameMap>();

		//Get private methods from GameMap
		CallGetSceneInfo=new Reflectors.RMethod<GameMap, NullObj>(GameMapGM, "GetSceneInfo");
		CallGetMapPosition=new Reflectors.RMethod<GameMap, Vector2>(GameMapGM, "GetMapPosition");
		CallGetMapZoneFromSceneName=new Reflectors.RMethod<GameMap, MapZone>(GameMapGM, "GetMapZoneFromSceneName");
	}

	//Static members
	private static readonly Transform GameMapT;
	private static readonly GameMap GameMapGM;

	//Local members
	public readonly GameMapScene GameMapScene;
	public readonly GameObject SceneGameObject;
	public readonly Vector2 ScenePos;
	public readonly Vector2 TileMapSize;
	public readonly string SceneName;

	//Get map position from game object position in scene
	public Vector2 GetMapPositionFromLocalPosition(Vector2 ObjPos) =>
		GameMapGM==null ? throw new MissingComponentException("Gamemap not set") :
		GetMapPosition(ObjPos, GameMapScene, SceneGameObject, ScenePos, TileMapSize);

	//Get map position from a game object
	public Vector2 GetMapPositionFromGameObject(GameObject PosObj) =>
		GetMapPositionFromLocalPosition(PosObj.transform.position);

	//Create a pin
	public Pin CreatePin(GameObject Obj) =>
		new(GetMapPositionFromGameObject(Obj), Obj.name);

	//Private functions from GameMap made available through reflection (and yes, I know we can access private members in BepInEx, but this is cleaner and foolproof)
	private class NullObj { }
	private static readonly Reflectors.RMethod<GameMap, NullObj> CallGetSceneInfo;
	private static readonly Reflectors.RMethod<GameMap, Vector2> CallGetMapPosition;
	private static readonly Reflectors.RMethod<GameMap, MapZone> CallGetMapZoneFromSceneName;
	private static void GetSceneInfo(string sceneName, MapZone mapZone, out GameMapScene foundScene, out GameObject foundSceneObj, out Vector2 foundScenePos)
	{
		GameMapScene? tempFoundScene=null;
		GameObject? tempFoundSceneObj=null;
		foundScenePos = default;
		object?[] Args=[sceneName, mapZone, tempFoundScene, tempFoundSceneObj, foundScenePos];
		_=CallGetSceneInfo.InvokeArr(Args);
		foundScene=(GameMapScene)Args[2]!;
		foundSceneObj=(GameObject)Args[3]!;
		foundScenePos=(Vector2)Args[4]!;
	}
	private static Vector2 GetMapPosition(Vector2 positionInScene, GameMapScene scene, GameObject sceneObj, Vector2 scenePos, Vector2 sceneSize) =>
		CallGetMapPosition.Invoke(positionInScene, scene, sceneObj, scenePos, sceneSize);
	private static MapZone GetMapZoneFromSceneName(string sceneName) =>
		CallGetMapZoneFromSceneName.Invoke(sceneName);

	/*Get data for use in the function:
		Vector2 GetMapPosition(Vector2 PositionInScene, SceneToMapVectors V) =>
			new(
				V.ScenePos.x+V.BoundsSpriteSize.x*V.SceneLocalScale.x*(PositionInScene.x/V.SceneSize.x-1/2f),
				V.ScenePos.y+V.BoundsSpriteSize.y*V.SceneLocalScale.y*(PositionInScene.y/V.SceneSize.y-1/2f)
			);
	*/
	private static Vector2 FailedVector=new(-99999f, -99999f);
	public class SceneToMapVectors(GetScenePinData SPD)
	{
		public readonly Vector2 BoundsSpriteSize=SPD.GameMapScene?.BoundsSprite?.bounds.size ?? FailedVector;
		public readonly Vector2 SceneLocalScale=SPD.GameMapScene?.transform.localScale ?? FailedVector;
		public readonly Vector2 ScenePos=SPD.ScenePos;
		public readonly Vector2 SceneSize=SPD.TileMapSize;
	}
	//The following can do the same thing as FindPins.SceneVectors, but SceneSize can only be gathered while the scene is loaded (AFAIK)
	public static System.Collections.Generic.Dictionary<string, SceneToMapVectors> GetAllSceneVectors() =>
		new Reflectors.RField<GameMap, System.Collections.IDictionary>(GameMapGM, "mapCaches").Get().Keys.Cast<string>().ToDictionary(SceneName => SceneName, SceneName => {
			try { return new SceneToMapVectors(new GetScenePinData(SceneName)); }
			catch(System.Exception e) { Log.Error($"Failure extracting scene “{SceneName}”: {e.Message}"); return null!; }
		});
}