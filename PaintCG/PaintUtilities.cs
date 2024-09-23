using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PaintCG {
	public class PaintUtilities {

		// Função para rotacionar um ponto em torno de um ponto central por um ângulo específico
		public static Point RotatePoint(Point pointToRotate, Point centerPoint, double angleInDegrees) {
			// Converte o ângulo de graus para radianos, pois as funções trigonométricas utilizam radianos
			double angleInRadians = angleInDegrees * (Math.PI / 180);

			// Calcula os valores de cosseno e seno para o ângulo dado
			double cosTheta = Math.Cos(angleInRadians);
			double sinTheta = Math.Sin(angleInRadians);

			// Retorna o novo ponto rotacionado, aplicando a transformação de rotação
			return new Point {
				// A nova coordenada X é calculada com base nas fórmulas de rotação em torno de um ponto central
				X = (cosTheta * (pointToRotate.X - centerPoint.X) -
					 sinTheta * (pointToRotate.Y - centerPoint.Y) + centerPoint.X),
				// A nova coordenada Y é calculada de forma semelhante, usando seno e cosseno
				Y = (sinTheta * (pointToRotate.X - centerPoint.X) +
					 cosTheta * (pointToRotate.Y - centerPoint.Y) + centerPoint.Y)
			};
		}

		// Função para rotacionar um retângulo em torno de um ponto central por um ângulo específico
		public static void RotateRectangle(Rectangle rect, Point center, double angle) {
			// Remove a transformação existente, se houver
			rect.RenderTransform = null;

			// Cria uma nova transformação de rotação
			RotateTransform rotation = new RotateTransform(angle, center.X - Canvas.GetLeft(rect), center.Y - Canvas.GetTop(rect));

			// Aplica a nova transformação
			rect.RenderTransform = rotation;
		}

		// Função para atualizar o texto dos valores de escala
		public static void UpdateScaleValueText(Tuple<TextBlock, TextBlock> textBlocks, Tuple<Slider, Slider> sliders) {
			if (textBlocks.Item1 != null && textBlocks.Item2 != null) {
				textBlocks.Item1.Text = $"{sliders.Item1.Value:F2}x";
				textBlocks.Item2.Text = $"{sliders.Item2.Value:F2}x";
			}
		}

		// Função auxiliar para desenhar um pixel
		public static void DrawPixel(Point point, Canvas drawingCanvas, SolidColorBrush brushColor) {

			Ellipse pixel = new Ellipse {
				Width = 1.5,
				Height = 1.5,
				Fill = brushColor
			};
			Canvas.SetLeft(pixel, point.X);
			Canvas.SetTop(pixel, point.Y);
			drawingCanvas.Children.Add(pixel);
		}
	}
}
