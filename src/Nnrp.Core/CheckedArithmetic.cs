namespace Nnrp.Core
{
    public static class CheckedArithmetic
    {
        public static bool TryAdd(int left, int right, out int result)
        {
            result = 0;
            try
            {
                checked
                {
                    result = left + right;
                }

                return true;
            }
            catch (System.OverflowException)
            {
                return false;
            }
        }

        public static bool TryAdd(uint left, uint right, out uint result)
        {
            result = 0;
            try
            {
                checked
                {
                    result = left + right;
                }

                return true;
            }
            catch (System.OverflowException)
            {
                return false;
            }
        }

        public static bool TryAdd(ulong left, ulong right, out ulong result)
        {
            result = 0;
            try
            {
                checked
                {
                    result = left + right;
                }

                return true;
            }
            catch (System.OverflowException)
            {
                return false;
            }
        }
    }
}
