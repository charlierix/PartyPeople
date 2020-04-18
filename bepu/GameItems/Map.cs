using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Core;

namespace GameItems
{
    public class Map
    {
        private readonly List<IMapObject> _items = new List<IMapObject>();

        /// <summary>
        /// Adds an item to the map
        /// </summary>
        public void AddItem<T>(T item) where T : IMapObject
        {
            //TODO: Store by type, similar to the newt map

            _items.Add(item);

        }
        public bool RemoveItem<T>(T item, bool isFinalType = false, bool shouldDispose = true) where T : IMapObject
        {
            //TODO: Raise a removed event, similar to the newt map

            var removed = _items.RemoveWhere(o => o.Equals(item));

            return removed.Count() > 0;
        }
        ///// <summary>
        ///// This overload is a find and remove.  It's useful for when listening to collision events.  Some types need to always
        ///// be removed when collided, but all that's passed in is the body
        ///// </summary>
        ///// <remarks>
        ///// This is a copy/tweak of the other overload
        ///// </remarks>
        ///// <returns>
        ///// The item that was removed (or null if not found)
        ///// </returns>
        //public T RemoveItem<T>(Body physicsBody, bool shouldDispose = true) where T : IMapObject
        //{

        //int BodyHandle { get; set; }
        //BodyReference Body { get; set; }

        //}

        public IEnumerable<IMapObject> GetAll()
        {
            return _items.ToArray();
        }

        private static void SearchTrees()
        {
            //Swarm may want a map with snapshots

            //Here is a comparison of VPTree, KDTree, Octree
            //https://pdfs.semanticscholar.org/31eb/b51c67055721ad6527b6a16df75445b18494.pdf



            // One type of problem is something like gravity.  There's probably a need for large distance gravity and close distance gravity




            // It looks like it's between vptree and kdtree.  How easy is it to modify an existing tree for a while before needing to rebuild?



            //Accord.Collections.VPTree vptree;

            // The pdf said kdtree worked best for their test case.  An article was saying this isn't good for high dimensional data
            //Accord.Collections.KDTree kdtree;



            // This is hardcoded to 3D structures, not sure if it's worth reimplementing
            //or maybe octree


            // sptree doesn't make sense for storing points.  It stores contiguous memory pages to help query performance, or searching for files, or something like that
            //Accord.Collections.SPTree sptree;


        }

    }
}
