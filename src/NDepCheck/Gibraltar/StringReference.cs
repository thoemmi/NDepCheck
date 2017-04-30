#region File Header

using System;
using System.Collections.Generic;
#if DEBUG_GIBRALTAR
using System.Diagnostics;
#endif

#endregion File Header

// ReSharper disable once CheckNamespace -- derived from "Single Instance String Store for .NET" at 
// https://www.codeproject.com/Articles/38057/Single-Instance-String-Store-for-NET

namespace Gibraltar {
    public class Intern {
        private static readonly List<Dictionary<int, LinkedList<WeakReference>>> _allReferences = new List<Dictionary<int, LinkedList<WeakReference>>> ();

        protected static void AddForClearing(Dictionary<int, LinkedList<WeakReference>> references) {
            _allReferences.Add(references);
        }

        public static void ResetAll() {
            foreach (var d in _allReferences) {
                d.Clear();
            }
        }
    }

    /// <summary>
    /// Provides a way to ensure a string is a reference to a single copy instead of creating multiple copies.
    /// </summary>
    public class Intern<T> : Intern where T : class {
        // ReSharper disable StaticMemberInGenericType
        private static readonly Dictionary<int, LinkedList<WeakReference>> _references = new Dictionary<int, LinkedList<WeakReference>>(1024);
        private static readonly object _lock = new object(); //Multithread Protection lock

        static Intern() {
            AddForClearing(_references);
        }

        private static volatile bool _disableCache;
        // ReSharper restore StaticMemberInGenericType

        /// <summary>
        /// Indicates if the reference cache is disabled.
        /// </summary>
        /// <remarks>When disabled each method returns immediately and the input string is returned.  This allows comparision of 
        /// behavior with and without the cache without changing code.</remarks>
        public static bool Disabled {
            get {
                return _disableCache;
            }
            set {
                _disableCache = value;
            }
        }

        /// <summary>
        /// Swap the provided string for its common reference
        /// </summary>
        /// <param name="baseline">The string to be looked up and exchanged for its reference</param>
        /// <remarks>If the baseline isn't already in the reference cache it will be added.  The cache is automatically pruned to
        /// prevent it from consuming excess memory.</remarks>
        public static void SwapReference(ref T baseline) {
            baseline = GetReference(baseline);
        }

        /// <summary>
        /// Get the reference value for the provided baseline value.
        /// </summary>
        /// <param name="baseline"></param>
        /// <returns></returns>
        public static T GetReference(T baseline) {
            if (_disableCache)
                return baseline;

            if (baseline == null)
                return null;  //can't do a reference to null anyway

            //if (baseline.Length == 0) {
            //    return string.Empty;//this is a stock intered string.
            //}

            T originalValue = baseline;
            T referenceValue = null;
            try {
                lock (_lock) {
                    int baselineHashCode = baseline.GetHashCode();
                    LinkedList<WeakReference> possibleReferencesList;
                    if (_references.TryGetValue(baselineHashCode, out possibleReferencesList)) {
                        //hash code gets us close, now go through all of the items in the list to see if they're a match.
                        if (possibleReferencesList.Count > 0) {
                            LinkedListNode<WeakReference> currentReferenceNode = possibleReferencesList.First;
                            while (currentReferenceNode != null) //lets us clean up as we go.
                            {
                                var currentRefrence = currentReferenceNode.Value;

                                bool killNode = false;

                                if (currentRefrence == null) {
                                    //shouldn't happen, but makes everything else safe.
                                    killNode = true;
                                } else {
                                    //we don't bother checking the IsAlive option; since we need to do the real check below anyway that's just wasted time.

                                    //get this value and see if it's our man
                                    T possibleValue = currentRefrence.Target as T;

                                    //since the GC can run literally at any time, only now that we have a reference that would
                                    //keep the object around can we be confident it'll stay put.
                                    if (possibleValue == null) {
                                        //sneaky GC. It collected it just as we were adding our reference. Oh well, good as gone.
                                        killNode = true;
                                    } else {
                                        if (baseline.Equals(possibleValue)) {
                                            //this is our reference value - we're done and dusted!
                                            referenceValue = possibleValue;
                                            break; //we weren't going to kill the node, and no point in going any further.
                                        }
                                    }
                                }

                                //if this node shouldn't be around any more, lets remove it from the linked list
                                //which is an O(1) operation.
                                if (killNode) {
                                    LinkedListNode<WeakReference> victim = currentReferenceNode;
                                    currentReferenceNode = currentReferenceNode.Next; // so we can contiue our iteration
                                    possibleReferencesList.Remove(victim);
                                } else {
                                    currentReferenceNode = currentReferenceNode.Next; // so we can contiue our iteration
                                }
                            }
                        }
                    }

                    //ok.  If we didn't find an exiting a reference congratulations - this is the new reference.
                    if (referenceValue == null) {
                        referenceValue = originalValue;

                        //We need to put this reference in our cache - did we even find its hash location?
                        if (possibleReferencesList == null) {
                            //no, we did not.  we need to add that.
                            possibleReferencesList = new LinkedList<WeakReference>();
                            _references.Add(baselineHashCode, possibleReferencesList);
                        }

                        //now that we know wehave the list, we need to add a new node to it to be our item.
                        //we're going to add to the FRONT because we assume if we just used a string, we're most likely to just use it again.
                        possibleReferencesList.AddFirst(new WeakReference(referenceValue));
                    }

                    System.Threading.Monitor.PulseAll(_lock);
                }
            } catch (Exception ex) {
#if DEBUG_GIBRALTAR
                Debug.WriteLine("While trying to get the string reference for {0} an exception was thrown: {1}", baseline, ex.Message);
#endif
                GC.KeepAlive(ex); //just here to avoid a compiler warn in release mode

                //set the baseline back to what we originaly got because that's safe.
                referenceValue = originalValue;
            }

            return referenceValue;
        }

        /// <summary>
        /// Check the cache for garbage collected values.
        /// </summary>
        public static void Pack() {
#if DEBUG_GIBRALTAR
            Stopwatch packTimer = Stopwatch.StartNew();
#endif
            if (_disableCache)
                return;

            //Our caller has a right to expect to never get an exception from this.
            try {
                lock (_lock) {
                    if (_references.Count == 0)
                        return; //nothing here, nothing to collect.

                    List<int> deadNodes = new List<int>(_references.Count / 4 + 1); //assume we're going to wipe out 25% every time.

                    foreach (KeyValuePair<int, LinkedList<WeakReference>> keyValuePair in _references) {
                        LinkedList<WeakReference> currentReferencesList = keyValuePair.Value;
                        ReleaseCollectedNodes(currentReferencesList);
                        if (currentReferencesList.Count == 0) {
                            deadNodes.Add(keyValuePair.Key);
                        }
                    }

                    //and kill off our dead nodes.
                    foreach (int deadNodeKey in deadNodes) {
                        _references.Remove(deadNodeKey);
                    }
#if DEBUG_GIBRALTAR
                    packTimer.Stop();
                    Debug.WriteLine("StringReference: Removed " + deadNodes.Count + " nodes from the cache.");
                    Debug.WriteLine("StringReference:  Pack took {0}ms.", packTimer.ElapsedMilliseconds);
#endif
                    System.Threading.Monitor.PulseAll(_lock);
                }

            } catch (Exception ex) {
#if DEBUG_GIBRALTAR
                Trace.TraceError("While packing the string reference cache an exception was thrown: {0}", ex.Message);
#endif

                GC.KeepAlive(ex); //just here to avoid a compiler warn in release mode
            }
        }

        #region Private Properties and Methods

        private static void ReleaseCollectedNodes(LinkedList<WeakReference> list) {
            if (list.Count > 0) {
                LinkedListNode<WeakReference> currentReferenceNode = list.First;
                while (currentReferenceNode != null) //lets us clean up as we go.
                {
                    WeakReference currentRefrence = currentReferenceNode.Value;

                    bool killNode = false;

                    if (currentRefrence == null) {
                        //shouldn't happen, but makes everything else safe.
                        killNode = true;
                    }
                    //if this one has already been garbage collected, get rid of it.
                    else if (currentRefrence.Target == null) {
                        killNode = true;
                    }

                    //if this node shouldn't be around any more, lets remove it from the linked list
                    //which is an O(1) operation.
                    if (killNode) {
                        //cache the current node as our victim, move next, then remove the victim.  
                        //Order is important because move next won't work once removed.
                        LinkedListNode<WeakReference> victim = currentReferenceNode;
                        currentReferenceNode = currentReferenceNode.Next;
                        list.Remove(victim);
                    } else {
                        currentReferenceNode = currentReferenceNode.Next; // so we can continue our iteration
                    }
                }
            }
        }

        #endregion

        public static void Reset() {
            _references.Clear();
        }
    }
}
