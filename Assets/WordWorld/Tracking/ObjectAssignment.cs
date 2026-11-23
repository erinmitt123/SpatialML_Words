using System;

namespace WordWorld.Tracking
{
    /// <summary>
    /// Deterministic rectangular Hungarian assignment. Port of YoloObjectAssignment from PICO's
    /// yolo-mesh sample: matching all detections to all tracks at once stops two neighbouring
    /// same-class objects from stealing each other's detections, which greedy matching allows.
    /// Buffers are reused because this runs every inference frame.
    /// </summary>
    public sealed class ObjectAssignment
    {
        /// <summary>Cost of leaving a detection unmatched; real match costs stay well below it.</summary>
        public const double Unmatched = 1000.0;
        /// <summary>Cost of a pair that must never match; above Unmatched, so it is only used when forced.</summary>
        public const double Invalid = 1_000_000.0;

        private double[] m_costs = new double[0];
        private double[] m_rowPotentials = new double[0];
        private double[] m_columnPotentials = new double[0];
        private int[] m_columnRows = new int[0];
        private int[] m_predecessors = new int[0];
        private double[] m_minimum = new double[0];
        private bool[] m_visited = new bool[0];
        private int[] m_result = new int[0];
        private int m_rows, m_columns;

        /// <summary>Starts a problem; fill every cell with Set before Solve. Needs columns >= rows.</summary>
        public void Begin(int rows, int columns)
        {
            if (columns < rows) throw new ArgumentException("Assignment needs at least as many columns as rows.");
            m_rows = rows;
            m_columns = columns;
            Ensure(ref m_costs, rows * columns);
            Ensure(ref m_rowPotentials, rows + 1);
            Ensure(ref m_columnPotentials, columns + 1);
            Ensure(ref m_columnRows, columns + 1);
            Ensure(ref m_predecessors, columns + 1);
            Ensure(ref m_minimum, columns + 1);
            Ensure(ref m_visited, columns + 1);
            Ensure(ref m_result, rows);
        }

        public void Set(int row, int column, double cost) => m_costs[row * m_columns + column] = cost;

        /// <summary>Returns the chosen column for each row (valid until the next Begin).</summary>
        public int[] Solve()
        {
            Array.Clear(m_rowPotentials, 0, m_rows + 1);
            Array.Clear(m_columnPotentials, 0, m_columns + 1);
            Array.Clear(m_columnRows, 0, m_columns + 1);
            Array.Clear(m_predecessors, 0, m_columns + 1);
            for (int row = 1; row <= m_rows; row++) Augment(row);
            for (int row = 0; row < m_rows; row++) m_result[row] = -1;
            for (int column = 1; column <= m_columns; column++)
            {
                int row = m_columnRows[column];
                if (row > 0) m_result[row - 1] = column - 1;
            }
            return m_result;
        }

        private void Augment(int row)
        {
            // Index zero is a sentinel for the row currently being added to the matching.
            m_columnRows[0] = row;
            for (int c = 0; c <= m_columns; c++)
            {
                m_minimum[c] = double.PositiveInfinity;
                m_visited[c] = false;
            }
            int column = 0;
            do
            {
                m_visited[column] = true;
                int current = m_columnRows[column];
                double delta = double.PositiveInfinity;
                int next = 0;
                for (int candidate = 1; candidate <= m_columns; candidate++)
                {
                    if (m_visited[candidate]) continue;
                    double reduced = m_costs[(current - 1) * m_columns + candidate - 1] -
                                     m_rowPotentials[current] - m_columnPotentials[candidate];
                    if (reduced < m_minimum[candidate])
                    {
                        m_minimum[candidate] = reduced;
                        m_predecessors[candidate] = column;
                    }
                    // Strict comparison and ascending column order make equal-cost ties stable.
                    if (m_minimum[candidate] < delta)
                    {
                        delta = m_minimum[candidate];
                        next = candidate;
                    }
                }
                for (int c = 0; c <= m_columns; c++)
                {
                    if (m_visited[c])
                    {
                        m_rowPotentials[m_columnRows[c]] += delta;
                        m_columnPotentials[c] -= delta;
                    }
                    else
                    {
                        m_minimum[c] -= delta;
                    }
                }
                column = next;
            } while (m_columnRows[column] != 0);

            do
            {
                int previous = m_predecessors[column];
                m_columnRows[column] = m_columnRows[previous];
                column = previous;
            } while (column != 0);
        }

        private static void Ensure<T>(ref T[] buffer, int size)
        {
            if (buffer.Length < size) buffer = new T[Math.Max(size, buffer.Length * 2)];
        }
    }
}
