using System;
using System.Collections.Generic;
using System.Text;

namespace GameItems
{
    /// <summary>
    /// This is for parts that need to be regularly updated (like sensors)
    /// </summary>
    public interface IPartUpdatable
    {
        /// <summary>
        /// This is called on the same thread the object was created on.  This should do as little as possible.  Basically, just graphics
        /// </summary>
        void Update_MainThread(double elapsedTime);
        /// <summary>
        /// This is called on a random thread each time
        /// </summary>
        void Update_AnyThread(double elapsedTime);

        // These are hints for how often to call update.  These values need to be the same for any instance of that type (that way
        // optimizations can be done at the type level instead of evaluating each instance)
        //
        // A value of zero means don't skip any updates.  One would skip every other update, two would be 1 tick,
        // 2 skips, 1 tick, 2 skips, etc.  This way, items that don't need to be called as often can give larger skip values.
        //
        // A value of null means don't bother calling that method (no code inside that method)
        int? IntervalSkips_MainThread { get; }
        int? IntervalSkips_AnyThread { get; }
    }
}
