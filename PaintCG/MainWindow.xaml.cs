using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace PaintCG {
	public partial class MainWindow : Window {
		// Códigos de região para Cohen-Sutherland
		const int INSIDE = 0; // 0000
		const int LEFT = 1;   // 0001
		const int RIGHT = 2;  // 0010
		const int BOTTOM = 4; // 0100
		const int TOP = 8;    // 1000

		public double xmin, ymin, xmax, ymax;

		private Point? initialPoint = null;
		private Point? finalPoint = null;
		private Point? previousMousePosition = null;
		private Point selectionCenter;

		private SolidColorBrush brushColor = Brushes.Black;

		public DrawModeEnum DrawMode = DrawModeEnum.RetaDDA;
		public ClipModeEnum ClipMode = ClipModeEnum.NoClip;

		private TransformGroup transformGroup;
		private RotateTransform rotateTransform;
		private ScaleTransform scaleTransform;
		private Rectangle selectionRect;
		private Dictionary<Ellipse, Point> originalPositions = new Dictionary<Ellipse, Point>();
		private Size originalSelectionSize;

		List<Ellipse> selectedElements = new List<Ellipse>();
		List<ToggleButton> toggleButtons = new List<ToggleButton>();

		private bool isClipping = false;
		private bool acceptClipping = true;
		private bool isMovingSelection = false;
		private bool isReflected = false;
		private bool isDrawing = false;

		public MainWindow() {
			InitializeComponent();
			ColorDisplay.Fill = brushColor;

			// Inicialize o TransformGroup e RotateTransform
			transformGroup = new TransformGroup();
			rotateTransform = new RotateTransform();
			scaleTransform = new ScaleTransform();
			transformGroup.Children.Add(rotateTransform);
			transformGroup.Children.Add(scaleTransform);

			foreach (ToggleButton tb in DockPanelToggleButtons.Children.OfType<ToggleButton>()) {
				toggleButtons.Add(tb);
			}

			// Inicialize os valores dos sliders de escala
			ScaleXSlider.Value = 1;
			ScaleYSlider.Value = 1;
			PaintUtilities.UpdateScaleValueText(new Tuple<TextBlock, TextBlock>(ScaleXValueText, ScaleYValueText), new Tuple<Slider, Slider>(ScaleXSlider, ScaleYSlider));

		}

		// Eventos de clique do mouse
		private void DrawingCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
			// Captura a posição do clique
			Point clickedPoint = e.GetPosition(DrawingCanvas);

			if (DrawMode != DrawModeEnum.Desenhar) {
				if (isMovingSelection && selectedElements.Count > 0) {
					// Inicia o movimento ao clicar no canvas se houver elementos selecionados
					previousMousePosition = clickedPoint;
				} else {
					// Captura o ponto inicial para outras ações
					initialPoint = clickedPoint;
				}
			} else {
				isDrawing = true;
				previousMousePosition = clickedPoint;
			}
		}
		private void DrawingCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
			// Captura a posição do clique
			Point clickedPoint = e.GetPosition(DrawingCanvas);
			isDrawing = false;

			if (DrawMode != DrawModeEnum.Desenhar) {
				if (initialPoint != null && finalPoint == null) {
					// Defina o ponto final se o ponto inicial já foi definido e o ponto final ainda não foi definido
					finalPoint = clickedPoint;
				}
				if (!isMovingSelection) {
					Draw();
				} else {
					previousMousePosition = null;
					isMovingSelection = false;
				}
			}
		}

		// Função para desenhar a forma selecionada
		public void Draw() {
			Point p1 = initialPoint.Value;
			Point p2 = finalPoint.Value;

			switch (DrawMode) {
				case DrawModeEnum.RetaDDA:
					DrawLineDDA(p1, p2);
					break;
				case DrawModeEnum.RetaBresenham:
					DrawLineBresenham(p1, p2);
					break;
				case DrawModeEnum.Circunferencia:
					int radius = (int)Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
					DrawCircleBresenham(p1, radius);
					break;
				case DrawModeEnum.Retangulo:
					DrawRectangle(p1, p2);
					break;
				case DrawModeEnum.Selecionar:
					SelectRectangleArea(p1, p2);
					break;
			}

			// Limpa os pontos inicial e final
			initialPoint = null;
			finalPoint = null;
			acceptClipping = true;
		}

		// Algoritmo de recorte Liang-Barsky
		private bool LiangBarskyClip(ref Point p1, ref Point p2) {
			double x0 = p1.X, y0 = p1.Y, x1 = p2.X, y1 = p2.Y;
			double dx = x1 - x0;
			double dy = y1 - y0;

			double t0 = 0.0, t1 = 1.0;
			double p, q, r;

			// Verifica os quatro limites da janela de recorte
			for (int edge = 0; edge < 4; edge++) {
				if (edge == 0) { // Limite esquerdo
					p = -dx;
					q = x0 - xmin;
				} else if (edge == 1) { // Limite direito
					p = dx;
					q = xmax - x0;
				} else if (edge == 2) { // Limite inferior
					p = -dy;
					q = y0 - ymin;
				} else { // Limite superior
					p = dy;
					q = ymax - y0;
				}

				if (p == 0 && q < 0) {
					// A linha está paralela à borda e completamente fora
					return false;
				}

				if (p != 0) {
					r = q / p;
					if (p < 0) {
						// Vindo de fora para dentro
						if (r > t1) return false;
						else if (r > t0) t0 = r;
					} else if (p > 0) {
						// Vindo de dentro para fora
						if (r < t0) return false;
						else if (r < t1) t1 = r;
					}
				}
			}

			// Atualiza os pontos recortados se o segmento de linha estiver visível
			if (t1 < 1) {
				p2.X = x0 + t1 * dx;
				p2.Y = y0 + t1 * dy;
			}
			if (t0 > 0) {
				p1.X = x0 + t0 * dx;
				p1.Y = y0 + t0 * dy;
			}

			return true;
		}
		// Algoritmo de recorte Cohen-Sutherland
		private bool CohenSutherlandClip(ref Point p1, ref Point p2) {

			// Função para calcular o código de região
			int ComputeOutCode(double x, double y) {
				int code = INSIDE;

				if (x < xmin) code |= LEFT;
				else if (x > xmax) code |= RIGHT;
				if (y < ymin) code |= BOTTOM;
				else if (y > ymax) code |= TOP;

				return code;
			}

			double x0 = p1.X, y0 = p1.Y, x1 = p2.X, y1 = p2.Y;
			int outcode0 = ComputeOutCode(x0, y0);
			int outcode1 = ComputeOutCode(x1, y1);
			bool accept = false;

			while (true) {
				if ((outcode0 | outcode1) == 0) {
					// Caso trivial: completamente dentro
					accept = true;
					break;
				} else if ((outcode0 & outcode1) != 0) {
					// Caso trivial: completamente fora
					break;
				} else {
					// Caso intermediário: a linha precisa ser recortada
					double x, y;
					int outcodeOut = (outcode0 != 0) ? outcode0 : outcode1;

					if ((outcodeOut & TOP) != 0) {
						// Ponto está acima da área de recorte
						x = x0 + (x1 - x0) * (ymax - y0) / (y1 - y0);
						y = ymax;
					} else if ((outcodeOut & BOTTOM) != 0) {
						// Ponto está abaixo da área de recorte
						x = x0 + (x1 - x0) * (ymin - y0) / (y1 - y0);
						y = ymin;
					} else if ((outcodeOut & RIGHT) != 0) {
						// Ponto está à direita da área de recorte
						y = y0 + (y1 - y0) * (xmax - x0) / (x1 - x0);
						x = xmax;
					} else { // outcodeOut & LEFT
							 // Ponto está à esquerda da área de recorte
						y = y0 + (y1 - y0) * (xmin - x0) / (x1 - x0);
						x = xmin;
					}

					if (outcodeOut == outcode0) {
						x0 = x;
						y0 = y;
						outcode0 = ComputeOutCode(x0, y0);
					} else {
						x1 = x;
						y1 = y;
						outcode1 = ComputeOutCode(x1, y1);
					}
				}
			}

			if (accept) {
				// Atualiza os pontos recortados
				p1 = new Point(x0, y0);
				p2 = new Point(x1, y1);
			}

			return accept;
		}
		// Algoritmo de rasterização DDA
		private void DrawLineDDA(Point p1, Point p2) {
			double dx = p2.X - p1.X;
			double dy = p2.Y - p1.Y;
			double steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
			double xIncrement = dx / steps;
			double yIncrement = dy / steps;

			double x = p1.X;
			double y = p1.Y;

			for (int i = 0; i <= steps; i++) {
				// Desenha cada pixel como uma pequena elipse para representar o ponto
				PaintUtilities.DrawPixel(new Point(x, y), DrawingCanvas, brushColor);
				x += xIncrement;
				y += yIncrement;
			}
		}
		// Algoritmo de rasterização de Bresenham
		private void DrawLineBresenham(Point p1, Point p2) {
			int x0 = (int)p1.X;
			int y0 = (int)p1.Y;
			int x1 = (int)p2.X;
			int y1 = (int)p2.Y;

			int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
			int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
			int err = dx + dy, e2; // erro acumulado

			while (true) {
				// Desenha o pixel
				PaintUtilities.DrawPixel(new Point(x0, y0), DrawingCanvas, brushColor);

				if (x0 == x1 && y0 == y1) break;
				e2 = 2 * err;
				if (e2 >= dy) { err += dy; x0 += sx; }
				if (e2 <= dx) { err += dx; y0 += sy; }
			}
		}
		// Algoritmo de rasterização de círculo de Bresenham
		private void DrawCircleBresenham(Point center, int radius) {
			// Função auxiliar para desenhar os pontos simétricos da circunferência
			void DrawSymmetricPoints(Point center, int x, int y) {
				PaintUtilities.DrawPixel(new Point(center.X + x, center.Y + y), DrawingCanvas, brushColor);
				PaintUtilities.DrawPixel(new Point(center.X - x, center.Y + y), DrawingCanvas, brushColor);
				PaintUtilities.DrawPixel(new Point(center.X + x, center.Y - y), DrawingCanvas, brushColor);
				PaintUtilities.DrawPixel(new Point(center.X - x, center.Y - y), DrawingCanvas, brushColor);
				PaintUtilities.DrawPixel(new Point(center.X + y, center.Y + x), DrawingCanvas, brushColor);
				PaintUtilities.DrawPixel(new Point(center.X - y, center.Y + x), DrawingCanvas, brushColor);
				PaintUtilities.DrawPixel(new Point(center.X + y, center.Y - x), DrawingCanvas, brushColor);
				PaintUtilities.DrawPixel(new Point(center.X - y, center.Y - x), DrawingCanvas, brushColor);
			}

			int x = 0;
			int y = radius;
			int d = 3 - 2 * radius;

			DrawSymmetricPoints(center, x, y);  // Desenha os pontos simétricos
			while (y >= x) {
				x++;
				if (d > 0) {
					y--;
					d = d + 4 * (x - y) + 10;
				} else {
					d = d + 4 * x + 6;
				}
				DrawSymmetricPoints(center, x, y);  // Desenha os pontos simétricos em cada quadrante
			}
		}
		// Algoritmo para desenhar um retângulo
		private void DrawRectangle(Point p1, Point p2) {
			// Desenha a linha superior
			DrawLineDDA(p1, new Point(p2.X, p1.Y));
			// Desenha a linha inferior
			DrawLineDDA(new Point(p1.X, p2.Y), p2);
			// Desenha a linha esquerda
			DrawLineDDA(p1, new Point(p1.X, p2.Y));
			// Desenha a linha direita
			DrawLineDDA(new Point(p2.X, p1.Y), p2);
		}
		// Função auxiliar para selecionar uma área retangular
		private void SelectRectangleArea(Point p1, Point p2) {
			// Limpa a seleção anterior
			foreach (Ellipse pixel in selectedElements) {
				pixel.Fill = Brushes.Black;
			}
			selectedElements.Clear();

			// Remove o retângulo de seleção anterior, se existir
			if (selectionRect != null) {
				DrawingCanvas.Children.Remove(selectionRect);
			}

			// Cria um novo retângulo de seleção
			selectionRect = new Rectangle {
				Width = Math.Abs(p2.X - p1.X),
				Height = Math.Abs(p2.Y - p1.Y),
				Stroke = Brushes.Blue,
				StrokeThickness = 1,
				Name = "selectionRect"
			};
			Canvas.SetLeft(selectionRect, Math.Min(p1.X, p2.X));
			Canvas.SetTop(selectionRect, Math.Min(p1.Y, p2.Y));
			DrawingCanvas.Children.Add(selectionRect);

			// Calcula e armazena o centro do retângulo de seleção
			selectionCenter = new Point(
				Math.Min(p1.X, p2.X) + selectionRect.Width / 2,
				Math.Min(p1.Y, p2.Y) + selectionRect.Height / 2
			);

			// Reseta a rotação
			RotationSlider.Value = 0;
			rotateTransform.Angle = 0;

			// Seleciona os elementos dentro da área de seleção
			foreach (UIElement element in DrawingCanvas.Children) {
				if (element is Ellipse pixel) {
					double x = Canvas.GetLeft(pixel);
					double y = Canvas.GetTop(pixel);
					if (x >= Math.Min(p1.X, p2.X) && x <= Math.Max(p1.X, p2.X) &&
						y >= Math.Min(p1.Y, p2.Y) && y <= Math.Max(p1.Y, p2.Y)) {
						pixel.Fill = Brushes.Red;
						selectedElements.Add(pixel);
					}
				}
			}

			originalPositions.Clear();
			foreach (Ellipse element in selectedElements) {
				originalPositions[element] = new Point(Canvas.GetLeft(element), Canvas.GetTop(element));
			}

			originalSelectionSize = new Size(selectionRect.Width, selectionRect.Height);

			// Reseta os sliders de escala
			ScaleXSlider.Value = 1;
			ScaleYSlider.Value = 1;
			PaintUtilities.UpdateScaleValueText(new Tuple<TextBlock, TextBlock>(ScaleXValueText, ScaleYValueText), new Tuple<Slider, Slider>(ScaleXSlider, ScaleYSlider));
		}
		// Função para aplicar o recorte a todas as elipses presentes no DrawingCanvas
		private void ApplyClippingToSelection(ClipModeEnum clipMode) {
			if (DrawingCanvas.Children.Count == 0 || selectionRect == null) {
				return; // Se não houver elementos no canvas ou se o retângulo de clip não estiver definido, sai da função
			}

			// Obtém os limites do retângulo de clip
			xmin = Canvas.GetLeft(selectionRect);
			ymin = Canvas.GetTop(selectionRect);
			xmax = xmin + selectionRect.Width;
			ymax = ymin + selectionRect.Height;

			List<Ellipse> elementsToRemove = new List<Ellipse>();

			// Itera sobre todas as elipses no DrawingCanvas
			foreach (UIElement element in DrawingCanvas.Children) {
				if (element is Ellipse ellipse) {
					// Obtém a posição atual da elipse
					Point originalPosition = new Point(Canvas.GetLeft(ellipse), Canvas.GetTop(ellipse));
					Point finalPosition = new Point(originalPosition.X + ellipse.Width, originalPosition.Y + ellipse.Height);

					bool isOutside = false; // Variável para verificar se a elipse está fora do retângulo de clip

					// Aplica o algoritmo de recorte correspondente
					switch (clipMode) {
						case ClipModeEnum.CohenSutherland:
							isOutside = !CohenSutherlandClip(ref originalPosition, ref finalPosition);
							break;
						case ClipModeEnum.LiangBarsky:
							isOutside = !LiangBarskyClip(ref originalPosition, ref finalPosition);
							break;
					}

					// Se a elipse está fora do retângulo de clip, adicione-a à lista de remoção
					if (isOutside) {
						elementsToRemove.Add(ellipse);
					}
				}
			}

			// Remove as elipses que estão fora do retângulo de clip
			foreach (Ellipse element in elementsToRemove) {
				DrawingCanvas.Children.Remove(element);
			}

			// Limpa a seleção anterior
			foreach (Ellipse pixel in selectedElements) {
				pixel.Fill = Brushes.Black;
			}
			selectedElements.Clear();

			// Remove o retângulo de seleção anterior, se existir
			if (selectionRect != null) {
				DrawingCanvas.Children.Remove(selectionRect);
			}
		}

		// Função que aplica a reflexão aos elementos selecionados
		private void ApplyReflectionToSelection(ReflectionType reflectionType) {

			// Atualiza o dicionário de posições originais com as novas posições refletidas
			originalPositions.Clear();
			foreach (Ellipse element in selectedElements) {
				originalPositions[element] = new Point(Canvas.GetLeft(element), Canvas.GetTop(element));
			}

			// Verifica se há elementos selecionados e se o retângulo de seleção existe
			if (selectedElements.Count == 0 || selectionRect == null) {
				return; // Se não houver, sai da função
			}

			// Itera sobre todos os elementos selecionados
			foreach (Ellipse element in selectedElements) {
				// Obtém a posição original do elemento
				Point originalPosition = originalPositions[element];

				// Inicializa a posição refletida como a posição original
				Point reflectedPosition = originalPosition;

				// Aplica a reflexão com base no tipo (X, Y ou XY)
				switch (reflectionType) {
					case ReflectionType.X:
						// Reflete no eixo X, mantendo a coordenada X e invertendo a Y
						reflectedPosition.Y = 2 * selectionCenter.Y - originalPosition.Y;
						break;
					case ReflectionType.Y:
						// Reflete no eixo Y, mantendo a coordenada Y e invertendo a X
						reflectedPosition.X = 2 * selectionCenter.X - originalPosition.X;
						break;
					case ReflectionType.XY:
						// Reflete em ambos os eixos, invertendo tanto X quanto Y
						reflectedPosition.X = 2 * selectionCenter.X - originalPosition.X;
						reflectedPosition.Y = 2 * selectionCenter.Y - originalPosition.Y;
						break;
				}

				// Define a nova posição refletida do elemento no Canvas
				Canvas.SetLeft(element, reflectedPosition.X);
				Canvas.SetTop(element, reflectedPosition.Y);
			}

			// Atualiza o dicionário de posições originais com as novas posições refletidas
			originalPositions.Clear();
			foreach (Ellipse element in selectedElements) {
				originalPositions[element] = new Point(Canvas.GetLeft(element), Canvas.GetTop(element));
			}

			// Reseta os valores dos sliders de escala
			isReflected = true;
			ScaleXSlider.Value = 1;
			isReflected = true;
			ScaleYSlider.Value = 1;
			PaintUtilities.UpdateScaleValueText(new Tuple<TextBlock, TextBlock>(ScaleXValueText, ScaleYValueText), new Tuple<Slider, Slider>(ScaleXSlider, ScaleYSlider));

			// Atualiza a rotação do retângulo de seleção, se aplicável
			PaintUtilities.RotateRectangle(selectionRect, selectionCenter, rotateTransform.Angle);
		}
		// Função para aplicar a transformação (escala e rotação) aos elementos selecionados
		private void ApplyTransformToSelection() {
			// Obtém os valores de escala dos sliders
			double scaleX = ScaleXSlider.Value;
			double scaleY = ScaleYSlider.Value;

			// Itera sobre todos os elementos selecionados (elipses)
			foreach (Ellipse element in selectedElements) {
				// Obtém a posição original do elemento no dicionário
				Point originalPosition = originalPositions[element];

				// Calcula o centro do elemento com base em sua posição original
				Point elementCenter = new Point(
					originalPosition.X + element.Width / 2,
					originalPosition.Y + element.Height / 2
				);

				// Calcula a nova posição escalada, com base no centro da seleção
				Point scaledPosition = new Point(
					selectionCenter.X + (elementCenter.X - selectionCenter.X) * scaleX,
					selectionCenter.Y + (elementCenter.Y - selectionCenter.Y) * scaleY
				);

				// Aplica a rotação à nova posição escalada
				Point rotatedPosition = PaintUtilities.RotatePoint(scaledPosition, selectionCenter, rotateTransform.Angle);

				// Atualiza a posição do elemento no Canvas após a rotação e a escala
				Canvas.SetLeft(element, rotatedPosition.X - element.Width / 2);
				Canvas.SetTop(element, rotatedPosition.Y - element.Height / 2);
			}

			// Atualiza o tamanho do retângulo de seleção com base nos valores de escala
			double newWidth = originalSelectionSize.Width * scaleX;
			double newHeight = originalSelectionSize.Height * scaleY;

			// Reposiciona o retângulo de seleção para que ele mantenha o centro da seleção
			Canvas.SetLeft(selectionRect, selectionCenter.X - newWidth / 2);
			Canvas.SetTop(selectionRect, selectionCenter.Y - newHeight / 2);

			// Atualiza as dimensões do retângulo de seleção
			selectionRect.Width = newWidth;
			selectionRect.Height = newHeight;

			// Aplica a rotação ao retângulo de seleção
			PaintUtilities.RotateRectangle(selectionRect, selectionCenter, rotateTransform.Angle);
		}
		private void Liang_Click(object sender, RoutedEventArgs e) {
			ClipMode = ClipModeEnum.LiangBarsky;
			ToggleButtonsReverse(sender as ToggleButton);
			ApplyClippingToSelection(ClipModeEnum.LiangBarsky);
		}
		private void Cohen_Click(object sender, RoutedEventArgs e) {
			ClipMode = ClipModeEnum.CohenSutherland;
			ToggleButtonsReverse(sender as ToggleButton);
			ApplyClippingToSelection(ClipModeEnum.CohenSutherland);
		}
		private void Select_Click(object sender, RoutedEventArgs e) {
			DrawMode = DrawModeEnum.Selecionar;
			ToggleButtonsReverse(sender as ToggleButton);
		}
		private void Retangulo_Click(object sender, RoutedEventArgs e) {
			DrawMode = DrawModeEnum.Retangulo;
			ToggleButtonsReverse(sender as ToggleButton);
		}
		private void Limpar_Click(object sender, RoutedEventArgs e) {
			DrawingCanvas.Children.Clear();
		}
		private void DDA_Click(object sender, RoutedEventArgs e) {
			DrawMode = DrawModeEnum.RetaDDA;
			ToggleButtonsReverse(sender as ToggleButton);
		}
		private void BresenhamLine_Click(object sender, RoutedEventArgs e) {
			DrawMode = DrawModeEnum.RetaBresenham;
			ToggleButtonsReverse(sender as ToggleButton);
		}
		private void BresenhamCirc_Click(object sender, RoutedEventArgs e) {
			DrawMode = DrawModeEnum.Circunferencia;
			ToggleButtonsReverse(sender as ToggleButton);
		}
		private void Move_Click(object sender, RoutedEventArgs e) {
			isMovingSelection = !isMovingSelection;
			ToggleButtonsReverse(sender as ToggleButton);
		}
		private void Draw_Click(object sender, RoutedEventArgs e) {
			DrawMode = DrawModeEnum.Desenhar;
			ToggleButtonsReverse(sender as ToggleButton);
		}

		// Eventos de alteração de cor
		private void ColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e) {
			if (e.NewValue.HasValue) {
				brushColor = new SolidColorBrush(e.NewValue.Value);
				ColorDisplay.Fill = brushColor;
			}
		}

		// Eventos de transformação
		private void ReflectX_Click(object sender, RoutedEventArgs e) {
			ApplyReflectionToSelection(ReflectionType.X);
		}
		private void ReflectY_Click(object sender, RoutedEventArgs e) {
			ApplyReflectionToSelection(ReflectionType.Y);
		}
		private void ReflectXY_Click(object sender, RoutedEventArgs e) {
			ApplyReflectionToSelection(ReflectionType.XY);
		}

		private void ScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {

			if (!isReflected) {
				if (selectedElements.Count > 0 && selectionRect != null) {
					ApplyTransformToSelection();
				}
			}
			isReflected = false;

			PaintUtilities.UpdateScaleValueText(new Tuple<TextBlock, TextBlock>(ScaleXValueText, ScaleYValueText), new Tuple<Slider, Slider>(ScaleXSlider, ScaleYSlider));
		}
		private void RotationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {

			if (selectedElements.Count > 0 && selectionRect != null) {
				rotateTransform.Angle = e.NewValue;
				ApplyTransformToSelection();
			}

			RotationValueText.Text = $"{e.NewValue:F0}°";
		}
		private void DrawingCanvas_MouseMove(object sender, MouseEventArgs e) {
			if (DrawMode != DrawModeEnum.Desenhar) {
				if (isMovingSelection && selectedElements.Count > 0 && e.LeftButton == MouseButtonState.Pressed) {
					Point currentMousePosition = e.GetPosition(DrawingCanvas);

					if (previousMousePosition != null) {
						double deltaX = currentMousePosition.X - previousMousePosition.Value.X;
						double deltaY = currentMousePosition.Y - previousMousePosition.Value.Y;

						// Move cada elemento selecionado
						foreach (Ellipse element in selectedElements) {
							Canvas.SetLeft(element, Canvas.GetLeft(element) + deltaX);
							Canvas.SetTop(element, Canvas.GetTop(element) + deltaY);
						}

						// Move o retângulo de seleção
						if (selectionRect != null) {
							Canvas.SetLeft(selectionRect, Canvas.GetLeft(selectionRect) + deltaX);
							Canvas.SetTop(selectionRect, Canvas.GetTop(selectionRect) + deltaY);

							// Atualiza o centro de seleção
							selectionCenter.X += deltaX;
							selectionCenter.Y += deltaY;

							// Mantém a rotação do retângulo durante o movimento
							PaintUtilities.RotateRectangle(selectionRect, selectionCenter, rotateTransform.Angle);
						}
					}

					previousMousePosition = currentMousePosition;
				}
			} else {
				if (isDrawing) {
					Point currentPoint = e.GetPosition(DrawingCanvas);
					if (previousMousePosition != null) DrawLineDDA(previousMousePosition.Value, currentPoint);
					previousMousePosition = currentPoint;
				}
			}
		}

		private void ToggleButtonsReverse(ToggleButton currentToggleButton) {
			foreach (ToggleButton tb in toggleButtons) {
				if (tb != currentToggleButton) {
					tb.IsChecked = false;
				}
			}
		}
	}
}