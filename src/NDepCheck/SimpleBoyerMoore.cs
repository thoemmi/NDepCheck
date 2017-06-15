using System.Collections.Generic;

namespace NDepCheck {
    public static class SimpleBoyerMoore {
        public static Dictionary<T, int> CreateLastIndexCollection<T>(T[] sub) {
            var result = new Dictionary<T, int>();
            for (int i = 0; i < sub.Length; i++) {
                result[sub[i]] = i;
            }
            return result;
        }

        public static int IndexOf<T>(this T[] array, T[] sub, Dictionary<T, int> lastIndexCollection = null) {
            Dictionary<T, int> li = lastIndexCollection ?? CreateLastIndexCollection(sub);
            var possibleStartPos = 0;
            while (possibleStartPos + sub.Length < array.Length) {
                for (int j = sub.Length - 1; j >= 0; j--) {
                    T other = array[possibleStartPos + j];
                    if (Equals(other, sub[j])) {
                        if (j == 0) {
                            return possibleStartPos;
                        } else {
                            // continue with testing sub's elements against array
                        }
                    } else {
                        int otherPosInSub;
                        if (li.TryGetValue(other, out otherPosInSub)) {
                            if (otherPosInSub < j) {
                                // other occurs previously in sub - therefore, we can shift sub so that this previous occurrence 
                                // aligns with the current position.
                                // Example: array aaaaaxaaaaaa    possibleStartPos=2
                                //            sub   axabaa        j=3; as lastPos[x]=1, we can shift sub thus:
                                //                    axabaa    so new possibleStartPos = 4 = 2+3-1
                                //                   
                                possibleStartPos += j - otherPosInSub;
                            } else {
                                // other occurs later in sub; our simple data structure (the lastIndex dictionary) does not help
                                // us to find other's position in sub to the left of j, so we can just shift the attempt by 1.
                                // (real Boyer-Mooer has a more advanced data structure to do this)
                                possibleStartPos++;
                            }
                        } else {
                            // other does not occur in sub,- therefore, we can shift sub so that its start is after other's position
                            // Example: array aaaaaxaaaaaa    possibleStartPos=2
                            //            sub   aaabaa        j=3; as x does not occur in sub, we can shift sub thus:
                            //                      aaabaa    so new possibleStartPos = 6 = 2+3+1
                            //                   
                            possibleStartPos += j + 1;
                        }
                        // No match at j - we have increased possibleStartPos, now we must again start comparing all of sub.
                        break; // of for-j loop
                    }
                }
            }
            return -1;
        }
    }
}