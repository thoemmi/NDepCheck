//using System;
//using System.Collections.Generic;
//using System.Linq;

//namespace NDepCheck.Transforming.GraphTransformations {
//    public class HideOuterGraph <T> : IGraphTransformation<T> where T : class, Dependency  {
//        private readonly bool _incoming;
//        private readonly bool _outgoing;

//        public HideOuterGraph(IEnumerable<string> options) {
//            bool incomingSpecified = false;
//            bool outgoingSpecified = false;
//            bool directionSpecified = false;
//            foreach (var o in options) {
//                if (o == "i") {
//                    incomingSpecified = true;
//                    directionSpecified = true;
//                } else if (o == "o") {
//                    outgoingSpecified = true;
//                    directionSpecified = true;
//                } else {
//                    throw new ArgumentException("Unknown option for HideOuterGraph(HO): " + o);
//                }
//            }
//            _incoming = !directionSpecified || incomingSpecified;
//            _outgoing = !directionSpecified || outgoingSpecified;
//        }

//        public IEnumerable<T> Run(IEnumerable<T> edges) {
//            T[] visibleEdges = edges.Where(e => !e.Hidden).ToArray();

//            foreach (var e in visibleEdges.Where(e => !e.UsingItem.IsInner || !e.UsedItem.IsInner)) {
//                e.Hidden = true;
//            }
//            if (_incoming) {
//                foreach (var e in visibleEdges.Where(e => !e.UsingItem.IsInner && e.UsedItem.IsInner)) {
//                    e.Hidden = false;
//                }
//            }
//            if (_outgoing) {
//                foreach (var e in visibleEdges.Where(e => e.UsingItem.IsInner && !e.UsedItem.IsInner)) {
//                    e.Hidden = false;
//                }
//            }

//            return edges;
//        }

//        public string GetInfo() {
//            return "Hiding outer edges";
//        }
//    }
//}