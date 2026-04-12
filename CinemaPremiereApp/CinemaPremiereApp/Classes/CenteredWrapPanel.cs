using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CinemaPremiereApp.Classes
{
    public class CenteredWrapPanel : Panel
    {
        protected override Size MeasureOverride(Size constraint)
        {
            Size curLineSize = new Size();
            Size panelSize = new Size();

            foreach (UIElement child in InternalChildren)
            {
                child.Measure(constraint);
                Size size = child.DesiredSize;

                if (curLineSize.Width + size.Width > constraint.Width)
                {
                    panelSize.Width = Math.Max(panelSize.Width, curLineSize.Width);
                    panelSize.Height += curLineSize.Height;
                    curLineSize = size;
                }
                else
                {
                    curLineSize.Width += size.Width;
                    curLineSize.Height = Math.Max(curLineSize.Height, size.Height);
                }
            }

            panelSize.Width = Math.Max(panelSize.Width, curLineSize.Width);
            panelSize.Height += curLineSize.Height;
            return panelSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            int firstInLine = 0;
            Size curLineSize = new Size();
            double curY = 0;

            for (int i = 0; i < InternalChildren.Count; i++)
            {
                Size size = InternalChildren[i].DesiredSize;

                if (curLineSize.Width + size.Width > finalSize.Width)
                {
                    ArrangeLine(curY, curLineSize, firstInLine, i, finalSize.Width);
                    curY += curLineSize.Height;
                    curLineSize = size;
                    firstInLine = i;
                }
                else
                {
                    curLineSize.Width += size.Width;
                    curLineSize.Height = Math.Max(curLineSize.Height, size.Height);
                }
            }

            ArrangeLine(curY, curLineSize, firstInLine, InternalChildren.Count, finalSize.Width);
            return finalSize;
        }

        private void ArrangeLine(double y, Size lineSize, int start, int end, double fullWidth)
        {
            double x = (fullWidth - lineSize.Width) / 2;
            
            for (int i = start; i < end; i++)
            {
                UIElement child = InternalChildren[i];
                child.Arrange(new Rect(x, y, child.DesiredSize.Width, lineSize.Height));
                x += child.DesiredSize.Width;
            }
        }
    }
}
