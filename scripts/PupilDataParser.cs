using System.Collections.Generic;
using UnityEngine;

namespace BrainVR.Eyetracking.PupilLabs
{
    public static class PupilDataParser
    {
        public static void ParseTopic(string topic, Dictionary<string, object> dictionary)
        {
            if (topic.StartsWith("pupil")) ParsePupilTopic(dictionary);
        }

        public static void ParsePupilTopic(Dictionary<string, object> dictionary)
        {
            foreach (var item in dictionary)
            {
                switch (item.Key)
                {
                    case "topic":
                    case "method":
                    case "id":
                        var textForKey = PupilTools.StringFromDictionary(dictionary, item.Key);
                        // Do stuff
                        break;
                    case "confidence":
                    case "timestamp":
                    case "diameter":
                        var valueForKey = PupilTools.FloatFromDictionary(dictionary, item.Key);
                        // Do stuff
                        break;
                    case "norm_pos":
                        var positionForKey = PupilTools.VectorFromDictionary(dictionary, item.Key);
                        // Do stuff
                        break;
                    case "ellipse":
                        var dictionaryForKey = PupilTools.DictionaryFromDictionary(dictionary, item.Key);
                        foreach (var pupilEllipse in dictionaryForKey)
                        {
                            switch (pupilEllipse.Key.ToString())
                            {
                                case "angle":
                                    var angle = (float)(double)pupilEllipse.Value;
                                    // Do stuff
                                    break;
                                case "center":
                                case "axes":
                                    var vector = PupilTools.ObjectToVector(pupilEllipse.Value);
                                    // Do stuff
                                    break;
                                default:
                                    break;
                            }
                        }

                        break;
                    default:
                        break;
                }
            }
        }

        private static object IDo;
        public static Vector3 ObjectToVector(object source)
        {
            var position_o = source as object[];
            var result = Vector3.zero;
            if (position_o.Length != 2 && position_o.Length != 3) Debug.Log("Array length not supported");
            else
            {
                result.x = (float)(double)position_o[0];
                result.y = (float)(double)position_o[1];
                if (position_o.Length == 3) result.z = (float)(double)position_o[2];
            }
            return result;
        }

        public static Vector3 VectorFromDictionary(Dictionary<string, object> source, string key)
        {
            return source.ContainsKey(key) ? Position(source[key], false) : Vector3.zero;
        }
        public static Vector3 Position(object position, bool applyScaling)
        {
            var result = ObjectToVector(position);
            if (applyScaling) result /= PupilSettings.PupilUnitScalingFactor;
            return result;
        }
        public static float FloatFromDictionary(Dictionary<string, object> source, string key)
        {
            source.TryGetValue(key, out object valueO);
            return (float)(double)valueO;
        }
        public static string StringFromDictionary(Dictionary<string, object> source, string key)
        {
            var result = "";
            if (source.TryGetValue(key, out IDo))
                result = IDo.ToString();
            return result;
        }
        public static Dictionary<object, object> DictionaryFromDictionary(Dictionary<string, object> source, string key)
        {
            if (source.ContainsKey(key)) return source[key] as Dictionary<object, object>;
            return null;
        }

    }

}

