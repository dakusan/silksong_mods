import "./style.scss";
import $ from "jquery";
import { MapControl } from "./MapControl"

$(() => new  MapControl().Init("Assets/PAtlasMap.png"));