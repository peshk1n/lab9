using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using FastBitmap;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using System.Windows.Forms;
using static lab6.Form1;
using static System.Windows.Forms.DataFormats;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace lab6
{
    public partial class Form1 : Form
    {
        private Point3D vector_view;
        private bool IsPaint = true;
        Polyhedron polyhedron;
        private TransformationMatrix currentProjectionMatrix;
        private List<Point3D> line = new List<Point3D>();
        private bool IsPersp = true;
        int del = 50;
        private float[,] zValues;
        private Camera camera;
        private double[,] zBuffer;


        public Form1()
        {
            InitializeComponent();
            //polyhedron = CreateCube();

            pictureBox1.Paint += new PaintEventHandler(pictureBox1_Paint);

            txtOffsetX.KeyPress += ApplyTranslation;
            txtOffsetY.KeyPress += ApplyTranslation;
            txtOffsetZ.KeyPress += ApplyTranslation;

            txtRotationX.KeyPress += ApplyRotation;
            txtRotationY.KeyPress += ApplyRotation;
            txtRotationZ.KeyPress += ApplyRotation;

            txtScaleX.KeyPress += ApplyScaling;
            txtScaleY.KeyPress += ApplyScaling;
            txtScaleZ.KeyPress += ApplyScaling;

            firstPointX.KeyPress += ChangeFirstPoint;
            firstPointY.KeyPress += ChangeFirstPoint;
            firstPointZ.KeyPress += ChangeFirstPoint;

            secondPointX.KeyPress += ChangeSecondPoint;
            secondPointY.KeyPress += ChangeSecondPoint;
            secondPointZ.KeyPress += ChangeSecondPoint;

            cameraX.KeyPress += ChangeCameraAngle;
            cameraY.KeyPress += ChangeCameraAngle;
            cameraZ.KeyPress += ChangeCameraAngle;

            cameraX0.KeyPress += ChangeCameraPosition;
            cameraY0.KeyPress += ChangeCameraPosition;


            txtAngle.KeyPress += ChangeAngle;

            currentProjectionMatrix = TransformationMatrix.PerspectiveProjection(del);
            comboBox1.Items.Add("Центральная");
            comboBox1.Items.Add("Аксонометрическая");
            comboBox1.SelectedItem = "Центральная";
            comboBox1.SelectedIndex = 0;


            funcomboBox.Items.Add("sinX + cosY");
            funcomboBox.Items.Add("x^2+y^2");
            funcomboBox.Items.Add("5*(Cos(r)/r+0.1), r=x^2+y^2+1");
            funcomboBox.Items.Add("Cos(r)/(r+1), r=x^2+y^2");
            funcomboBox.Items.Add("Sin(x)*Cos(y);");

            camRot.KeyPress += CameraRotation;
            panel3.Enabled = panel3.Visible = false;
            camera = new Camera();
            //double r=x*x+y*y+1;
            //return 5*(Math.Cos(r)/r+0.1);double r = x * x + y * y;

            zBuffer = new double[pictureBox1.Width, pictureBox1.Height];

            pictureBox1.Invalidate();
        }

        // Класс точки
        public class Point3D
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Z { get; set; }
            public Point3D Normal { get; set; }

            public Point3D(double x, double y, double z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public static Point3D Vector(Point3D a, Point3D b)
            {
                return new Point3D(b.X - a.X, b.Y - a.Y, b.Z - a.Z);
            }

            // Метод для скалярного произведения
            public static double DotProduct(Point3D a, Point3D b)
            {
                return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
            }

            // Метод для векторного произведения
            public static Point3D CrossProduct(Point3D a, Point3D b)
            {
                return new Point3D(
                    a.Y * b.Z - a.Z * b.Y,
                    a.Z * b.X - a.X * b.Z,
                    a.X * b.Y - a.Y * b.X
                );
            }

            // Метод для нормализации вектора (приведение к единичной длине)
            public Point3D Normalize()
            {
                double length = Math.Sqrt(X * X + Y * Y + Z * Z);
                return new Point3D(X / length, Y / length, Z / length);
            }

            public double Length()
            {
                return Math.Sqrt(X * X + Y * Y + Z * Z);
            }
        }

        // Класс грани
        public class Face
        {
            private Random random = new Random();

            public List<int> Vertices { get; set; }
            public Color FaceColor { get; set; }
            public Point3D Normal { get; set; }

            public Face(List<int> vertices)
            {
                Vertices = vertices;
                FaceColor = Color.FromArgb(random.Next(256), 0, random.Next(256));
            }
        }

        // Класс многогранника
        public class Polyhedron
        {
            public List<Point3D> Vertices { get; set; } //точки
            public List<Face> Faces { get; set; } //грани

            public Polyhedron()
            {
                Vertices = new List<Point3D>();
                Faces = new List<Face>();
                CalculatePointNormals();
            }

            public Polyhedron(List<Point3D> vertices, List<Face> faces)
            {
                Vertices = vertices;
                Faces = faces;
                CalculatePointNormals();
            }

            // вычисление нормалей каждой вершины
            public void CalculatePointNormals()
            {
                CalculateNormals();
                for (int i=0; i<Vertices.Count; i++)
                {
                    //грани куда входит верщина с индексом i
                    var faces = Faces.Where(face => face.Vertices.Contains(i)).ToList();
                    Point3D normal = new Point3D(0, 0, 0);
                    foreach(Face face in faces)
                    {
                        normal.X += face.Normal.X;
                        normal.Y += face.Normal.Y;
                        normal.Z += face.Normal.Z;
                    }
                    Vertices[i].Normal = normal.Normalize();
                }
            }

            // Метод для вычисления нормалей каждой грани
            public void CalculateNormals()
            {
                foreach (var face in Faces)
                {
                    if (face.Vertices.Count >= 3) // Требуется минимум 3 вершины для определения нормали
                    {
                        // Получаем векторы двух сторон грани
                        var vector1 = Point3D.Vector(Vertices[face.Vertices[0]], Vertices[face.Vertices[1]]);
                        var vector2 = Point3D.Vector(Vertices[face.Vertices[0]], Vertices[face.Vertices[2]]);

                        // Вычисляем нормаль как векторное произведение двух векторов
                        //face.Normal = Point3D.CrossProduct(vector1, vector2);
                        face.Normal = Point3D.CrossProduct(vector1, vector2).Normalize();
                    }
                    else
                    {
                        face.Normal = new Point3D(0, 0, 0); // Если не хватает точек, нормаль по умолчанию
                    }
                }
            }
        }

        // Класс матрицы афинных преобразований
        // =========================================================================
        public class TransformationMatrix
        {
            public double[,] matrix = new double[4, 4];

            public TransformationMatrix()
            {
                matrix[0, 0] = 1;
                matrix[1, 1] = 1;
                matrix[2, 2] = 1;
                matrix[3, 3] = 1;
            }


            public Point3D Transform(Point3D p)
            {
                double x = matrix[0, 0] * p.X + matrix[1, 0] * p.Y + matrix[2, 0] * p.Z + matrix[3, 0];
                double y = matrix[0, 1] * p.X + matrix[1, 1] * p.Y + matrix[2, 1] * p.Z + matrix[3, 1];
                double z = matrix[0, 2] * p.X + matrix[1, 2] * p.Y + matrix[2, 2] * p.Z + matrix[3, 2];

                return new Point3D(x, y, z);
            }


            public Point3D TransformForPerspect(Point3D p)
            {
                double x = matrix[0, 0] * p.X + matrix[1, 0] * p.Y + matrix[2, 0] * p.Z + matrix[3, 0];
                double y = matrix[0, 1] * p.X + matrix[1, 1] * p.Y + matrix[2, 1] * p.Z + matrix[3, 1];
                double z = matrix[0, 2] * p.X + matrix[1, 2] * p.Y + matrix[2, 2] * p.Z + matrix[3, 2];
                double w = matrix[0, 3] * p.X + matrix[1, 3] * p.Y + matrix[2, 3] * p.Z + matrix[3, 3];

                if (w != 0)
                {
                    x /= w;
                    y /= w;
                    z /= w;
                }

                return new Point3D(x, y, z);
            }

            public static TransformationMatrix PerspectiveProjection(double distance)
            {
                // Перспективная проекция
                TransformationMatrix result = new TransformationMatrix();


                result.matrix[0, 0] = 1;
                result.matrix[1, 1] = 1;
                result.matrix[3, 2] = 1 / distance;
                result.matrix[3, 3] = 1;
                return result;
            }

            public static TransformationMatrix AxonometricProjection(double theta, double phi)
            {
                // преобразуем углы из градусов в радианы
                double cosTheta = Math.Cos(theta * Math.PI / 180);
                double sinTheta = Math.Sin(theta * Math.PI / 180);
                double cosPhi = Math.Cos(phi * Math.PI / 180);
                double sinPhi = Math.Sin(phi * Math.PI / 180);

                TransformationMatrix result = new TransformationMatrix();
                result.matrix[0, 0] = cosTheta;
                result.matrix[0, 1] = sinTheta * sinPhi;
                result.matrix[1, 1] = cosPhi;
                result.matrix[2, 0] = sinTheta;
                result.matrix[2, 1] = -cosTheta * sinPhi;
                result.matrix[2, 2] = 0;
                return result;
            }

            public static TransformationMatrix Multiply(TransformationMatrix a, TransformationMatrix b)
            {
                TransformationMatrix result = new TransformationMatrix();
                for (int i = 0; i < 4; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        result.matrix[i, j] = 0;
                        for (int k = 0; k < 4; k++)
                        {
                            result.matrix[i, j] += a.matrix[i, k] * b.matrix[k, j];
                        }
                    }
                }
                return result;
            }

            public static TransformationMatrix CreateTranslationMatrix(double dx, double dy, double dz)
            {
                TransformationMatrix result = new TransformationMatrix();
                result.matrix[3, 0] = dx;
                result.matrix[3, 1] = dy;
                result.matrix[3, 2] = dz;
                return result;
            }

            public static TransformationMatrix CreateScalingMatrix(double scaleX, double scaleY, double scaleZ)
            {
                TransformationMatrix result = new TransformationMatrix();
                result.matrix[0, 0] = scaleX;
                result.matrix[1, 1] = scaleY;
                result.matrix[2, 2] = scaleZ;
                return result;
            }

            public static TransformationMatrix CreateScalingMatrix(double scaleX, double scaleY, double scaleZ, Point3D center)
            {
                var translateToOrigin = CreateTranslationMatrix(-center.X, -center.Y, -center.Z);
                var scalingMatrix = CreateScalingMatrix(scaleX, scaleY, scaleZ);
                var translateBack = CreateTranslationMatrix(center.X, center.Y, center.Z);

                return Multiply(Multiply(translateToOrigin, scalingMatrix), translateBack);
            }


            public static TransformationMatrix CreateRotationMatrixX(double angle)
            {
                double radians = angle * Math.PI / 180;
                TransformationMatrix result = new TransformationMatrix();
                double cos = Math.Cos(radians);
                double sin = Math.Sin(radians);
                result.matrix[1, 1] = cos;
                result.matrix[1, 2] = -sin;
                result.matrix[2, 1] = sin;
                result.matrix[2, 2] = cos;
                return result;
            }

            public static TransformationMatrix CreateRotationMatrixY(double angle)
            {
                double radians = angle * Math.PI / 180;
                TransformationMatrix result = new TransformationMatrix();
                double cos = Math.Cos(radians);
                double sin = Math.Sin(radians);
                result.matrix[0, 0] = cos;
                result.matrix[0, 2] = -sin;
                result.matrix[2, 0] = sin;
                result.matrix[2, 2] = cos;
                return result;
            }

            public static TransformationMatrix CreateRotationMatrixY(double angle, Point3D center)
            {
                TransformationMatrix translateToOrigin = CreateTranslationMatrix(-center.X, -center.Y, -center.Z);
                TransformationMatrix rotationMatrix = CreateRotationMatrixY(angle);
                TransformationMatrix translateBack = CreateTranslationMatrix(center.X, center.Y, center.Z);
                return Multiply(Multiply(translateToOrigin, rotationMatrix), translateBack);
            }

            public static TransformationMatrix CreateRotationMatrixZ(double angle)
            {
                double radians = angle * Math.PI / 180;
                TransformationMatrix result = new TransformationMatrix();
                double cos = Math.Cos(radians);
                double sin = Math.Sin(radians);
                result.matrix[0, 0] = cos;
                result.matrix[0, 1] = sin;
                result.matrix[1, 0] = -sin;
                result.matrix[1, 1] = cos;
                return result;
            }

            public static TransformationMatrix CreateReverseRotationMatrix(double angleX, double angleY, double angleZ)
            {
                var rotationX = CreateRotationMatrixX(angleX);
                var rotationY = CreateRotationMatrixY(angleY);
                var rotationZ = CreateRotationMatrixZ(angleZ);
                return Multiply(Multiply(rotationZ, rotationY), rotationX);
            }

            public static TransformationMatrix CreateReverseRotationMatrix(double angleX, double angleY, double angleZ, Point3D center)
            {
                TransformationMatrix translateToOrigin = CreateTranslationMatrix(-center.X, -center.Y, -center.Z);
                TransformationMatrix rotationMatrix = CreateReverseRotationMatrix(angleX, angleY, angleZ);
                TransformationMatrix translateBack = CreateTranslationMatrix(center.X, center.Y, center.Z);
                return Multiply(Multiply(translateToOrigin, rotationMatrix), translateBack);
            }

            public static TransformationMatrix CreateRotationMatrix(double angleX, double angleY, double angleZ)
            {
                var rotationX = CreateRotationMatrixX(angleX);
                var rotationY = CreateRotationMatrixY(angleY);
                var rotationZ = CreateRotationMatrixZ(angleZ);
                return Multiply(Multiply(rotationX, rotationY), rotationZ);
            }

            public static TransformationMatrix CreateRotationAroundAxis(double angleX, double angleY, double angleZ, Point3D center)
            {
                TransformationMatrix translateToOrigin = CreateTranslationMatrix(-center.X, -center.Y, -center.Z);
                TransformationMatrix rotationMatrix = CreateRotationMatrix(angleX, angleY, angleZ);
                TransformationMatrix translateBack = CreateTranslationMatrix(center.X, center.Y, center.Z);
                return Multiply(Multiply(translateToOrigin, rotationMatrix), translateBack);
            }


            public static TransformationMatrix CreateReflectionMatrixXY()
            {
                TransformationMatrix result = new TransformationMatrix();
                result.matrix[2, 2] = -1;  // Отражение относительно плоскости XY меняет знак координаты Z
                return result;
            }

            public static TransformationMatrix CreateReflectionMatrixXY(Point3D center)
            {
                TransformationMatrix translateToOrigin = CreateTranslationMatrix(-center.X, -center.Y, -center.Z);
                TransformationMatrix reflectionMatrix = CreateReflectionMatrixXY();
                TransformationMatrix translateBack = CreateTranslationMatrix(center.X, center.Y, center.Z);
                return Multiply(Multiply(translateToOrigin, reflectionMatrix), translateBack);
            }


            public static TransformationMatrix CreateReflectionMatrixXZ()
            {
                TransformationMatrix result = new TransformationMatrix();
                result.matrix[1, 1] = -1;  // Отражение относительно плоскости XZ меняет знак координаты Y
                return result;
            }

            public static TransformationMatrix CreateReflectionMatrixXZ(Point3D center)
            {
                TransformationMatrix translateToOrigin = CreateTranslationMatrix(-center.X, -center.Y, -center.Z);
                TransformationMatrix reflectionMatrix = CreateReflectionMatrixXZ();
                TransformationMatrix translateBack = CreateTranslationMatrix(center.X, center.Y, center.Z);
                return Multiply(Multiply(translateToOrigin, reflectionMatrix), translateBack);
            }

            public static TransformationMatrix CreateReflectionMatrixYZ()
            {
                TransformationMatrix result = new TransformationMatrix();
                result.matrix[0, 0] = -1;  // Отражение относительно плоскости YZ меняет знак координаты X
                return result;
            }

            public static TransformationMatrix CreateReflectionMatrixYZ(Point3D center)
            {
                TransformationMatrix translateToOrigin = CreateTranslationMatrix(-center.X, -center.Y, -center.Z);
                TransformationMatrix reflectionMatrix = CreateReflectionMatrixYZ();
                TransformationMatrix translateBack = CreateTranslationMatrix(center.X, center.Y, center.Z);
                return Multiply(Multiply(translateToOrigin, reflectionMatrix), translateBack);
            }

            public static TransformationMatrix CreateRotationAroundLine(Point3D p1, Point3D p2, double angle)
            {
                double n = p2.X - p1.X;
                double m = p2.Y - p1.Y;
                double l = p2.Z - p1.Z;

                double length = Math.Sqrt(l * l + m * m + n * n);
                l /= length;
                m /= length;
                n /= length;
                double d = Math.Sqrt(m * m + n * n);

                double cosY = l;
                double sinY = -d;

                var rotateY = new TransformationMatrix();
                rotateY.matrix[0, 0] = cosY;
                rotateY.matrix[0, 2] = sinY;
                rotateY.matrix[2, 0] = -sinY;
                rotateY.matrix[2, 2] = cosY;

                double cosX = n / d;
                double sinX = m / d;

                var rotateX = new TransformationMatrix();
                rotateX.matrix[1, 1] = cosX;
                rotateX.matrix[1, 2] = -sinX;
                rotateX.matrix[2, 1] = sinX;
                rotateX.matrix[2, 2] = cosX;

                var translateToOrigin = CreateTranslationMatrix(-p1.X, -p1.Y, -p1.Z);
                var rotationAroundZ = CreateRotationMatrixZ(angle);
                var translateBack = CreateTranslationMatrix(p1.X, p1.Y, p1.Z);

                var inverseRotateY = new TransformationMatrix();
                inverseRotateY.matrix[0, 0] = cosY;
                inverseRotateY.matrix[0, 2] = -sinY;
                inverseRotateY.matrix[2, 0] = sinY;
                inverseRotateY.matrix[2, 2] = cosY;

                var inverseRotateX = new TransformationMatrix();
                inverseRotateX.matrix[1, 1] = cosX;
                inverseRotateX.matrix[1, 2] = sinX;
                inverseRotateX.matrix[2, 1] = -sinX;
                inverseRotateX.matrix[2, 2] = cosX;

                return Multiply(Multiply(Multiply(Multiply(Multiply(Multiply(translateToOrigin, rotateY), rotateX), rotationAroundZ),
                    inverseRotateX), inverseRotateY), translateBack);
            }
        }

        //----------------------------------------------------------------
        //куб
        public Polyhedron CreateCube()
        {
            double a = 100.0;
            //var vertices = new List<Point3D>
            //{
            //    new Point3D(0, 0, 0),
            //    new Point3D(a, 0, 0),
            //    new Point3D(a, a, 0),
            //    new Point3D(0, a, 0),
            //    new Point3D(0, 0, a),
            //    new Point3D(a, 0, a),
            //    new Point3D(a, a, a),
            //    new Point3D(0, a, a)
            //};

            //var faces = new List<Face>
            //{
            //    new Face(new List<int> { 0, 1, 2, 3 }),
            //    new Face(new List<int> { 4, 5, 6, 7 }),
            //    new Face(new List<int> { 0, 1, 5, 4 }),
            //    new Face(new List<int> { 1, 2, 6, 5 }),
            //    new Face(new List<int> { 2, 3, 7, 6 }),
            //    new Face(new List<int> { 3, 0, 4, 7 })
            //};

            var vertices = new List<Point3D>
            {
                new Point3D(-a, -a, a),
                new Point3D(-a, a, a),
                new Point3D(-a, -a, -a),
                new Point3D(-a, a, -a),
                new Point3D(a, -a, a),
                new Point3D(a, a, a),
                new Point3D(a, -a, -a),
                new Point3D(a, a, -a)
            };

            var faces = new List<Face>
            {
                new Face(new List<int> { 0, 1, 3, 2 }),
                new Face(new List<int> { 2, 3, 7, 6 }),
                new Face(new List<int> { 6, 7, 5, 4 }),
                new Face(new List<int> { 4, 5, 1, 0 }),
                new Face(new List<int> { 2, 6, 4, 0 }),
                new Face(new List<int> { 7, 3, 1, 5 })
            };
            double offsetX = pictureBox1.Width / 2;
            double offsetY = pictureBox1.Height / 2;
            for (int i = 0; i < vertices.Count; i++)
            {
                vertices[i] = new Point3D(vertices[i].X + offsetX, vertices[i].Y + offsetY, vertices[i].Z);
            }

            return new Polyhedron(vertices, faces);
        }

        //икосаэдр
        public Polyhedron CreateIcosahedron()
        {
            List<Point3D> vertices = new List<Point3D>();
            List<Face> faces = new List<Face>();

            double radius = 100.0;
            double height = radius / 2.0;
            double sqrt5 = Math.Sqrt(5) / 2.0 * radius;

            // нижняя окружность Z = -height
            for (int i = 0; i < 5; i++)
            {
                double angle = 2 * Math.PI * i / 5;
                double x = radius * Math.Cos(angle);
                double y = radius * Math.Sin(angle);
                vertices.Add(new Point3D(x, y, -height));
            }

            // верхняя окружность Z = +height
            for (int i = 0; i < 5; i++)
            {
                double angle = 2 * Math.PI * (i + 0.5) / 5; // Смещаем на полуградуса
                double x = radius * Math.Cos(angle);
                double y = radius * Math.Sin(angle);
                vertices.Add(new Point3D(x, y, height));
            }

            // Добавляем две вершины на оси Z
            vertices.Add(new Point3D(0, 0, sqrt5));  // Верхняя вершина Z = sqrt(5)/2 * radius
            vertices.Add(new Point3D(0, 0, -sqrt5)); // Нижняя вершина Z = -sqrt(5)/2 * radius

            // Создаем грани
            for (int i = 0; i < 5; i++)
            {
                int next = (i + 1) % 5;

                // Соединяем нижний и верхний пояс
                faces.Add(new Face(new List<int> { i, next, i + 5 }));
                faces.Add(new Face(new List<int> { next, next + 5, i + 5 }));
            }

            // Соединяем верхнюю и нижнюю вершины с поясом
            for (int i = 0; i < 5; i++)
            {
                faces.Add(new Face(new List<int> { 10, i + 5, (i + 1) % 5 + 5 })); // Верхняя вершина с верхним поясом
                faces.Add(new Face(new List<int> { 11, i, (i + 1) % 5 }));          // Нижняя вершина с нижним поясом
            }

            // Центрирование координат
            double offsetX = pictureBox1.Width / 2;
            double offsetY = pictureBox1.Height / 2;
            for (int i = 0; i < vertices.Count; i++)
            {
                vertices[i] = new Point3D(vertices[i].X + offsetX, vertices[i].Y + offsetY, vertices[i].Z);
            }

            return new Polyhedron(vertices, faces);
        }

        //додекаэдр
        public Polyhedron CreateDodecahedron()
        {
            double phi = (1 + Math.Sqrt(5)) / 2;
            double a = 100.0;

            List<Point3D> vertices = new List<Point3D>
            {
                new Point3D( a,  a,  a), //0
                new Point3D( a,  a, -a), //1
                new Point3D( a, -a,  a), //2
                new Point3D( a, -a, -a), //3
                new Point3D(-a,  a,  a), //4
                new Point3D(-a,  a, -a), //5
                new Point3D(-a, -a,  a), //6
                new Point3D(-a, -a, -a), //7
                new Point3D( 0,  1/phi * a,  phi * a), //8
                new Point3D( 0,  1/phi * a, -phi * a), //9
                new Point3D( 0, -1/phi * a,  phi * a), //10
                new Point3D( 0, -1/phi * a, -phi * a), //11
                new Point3D( 1/phi * a,  phi * a,  0), //12
                new Point3D(-1/phi * a,  phi * a,  0), //13
                new Point3D( 1/phi * a, -phi * a,  0), //14
                new Point3D(-1/phi * a, -phi * a,  0), //15
                new Point3D( phi * a,  0,  1/phi * a), //16
                new Point3D(-phi * a,  0,  1/phi * a), //17
                new Point3D( phi * a,  0, -1/phi * a), //18
                new Point3D(-phi * a,  0, -1/phi * a) //19
            };

            List<Face> faces = new List<Face>
            {
                new Face(new List<int> { 0, 8, 4, 13, 12 }),
                new Face(new List<int> { 16, 18, 3, 14, 2 }),
                new Face(new List<int> { 19, 5, 9, 11, 7 }),
                new Face(new List<int> { 6, 10, 2, 14, 15 }),
                new Face(new List<int> { 12, 13, 5, 9, 1 }),
                new Face(new List<int> { 0, 8 ,10, 2, 16 }),
                new Face(new List<int> { 13, 4, 17, 19, 5 }),
                new Face(new List<int> { 18, 1, 9, 11, 3 }),
                new Face(new List<int> { 0, 12, 1, 18, 16 }),
                new Face(new List<int> { 19, 17, 6, 15, 7 }),
                new Face(new List<int> { 3, 14, 15, 7, 11 }),
                new Face(new List<int> { 8, 4, 17, 6, 10 }),
            };

            double offsetX = pictureBox1.Width / 2;
            double offsetY = pictureBox1.Height / 2;
            for (int i = 0; i < vertices.Count; i++)
            {
                vertices[i] = new Point3D(vertices[i].X + offsetX, vertices[i].Y + offsetY, vertices[i].Z);
            }

            return new Polyhedron(vertices, faces);
        }
        //октаэдр
        public Polyhedron CreateOctahedron()
        {
            List<Point3D> vertices = new List<Point3D>();
            List<Face> faces = new List<Face>();

            double halfSide = 100.0;

            double offsetX = pictureBox1.Width / 2;
            double offsetY = pictureBox1.Height / 2;

            vertices.Add(new Point3D(halfSide + offsetX, 0 + offsetY, 0));
            vertices.Add(new Point3D(-halfSide + offsetX, 0 + offsetY, 0));
            vertices.Add(new Point3D(0 + offsetX, halfSide + offsetY, 0));
            vertices.Add(new Point3D(0 + offsetX, -halfSide + offsetY, 0));
            vertices.Add(new Point3D(0 + offsetX, 0 + offsetY, halfSide));
            vertices.Add(new Point3D(0 + offsetX, 0 + offsetY, -halfSide));


            faces.Add(new Face(new List<int> { 0, 2, 4 }));
            faces.Add(new Face(new List<int> { 0, 4, 3 }));
            faces.Add(new Face(new List<int> { 0, 3, 5 }));
            faces.Add(new Face(new List<int> { 0, 5, 2 }));

            faces.Add(new Face(new List<int> { 1, 4, 2 }));
            faces.Add(new Face(new List<int> { 1, 3, 4 }));
            faces.Add(new Face(new List<int> { 1, 5, 3 }));
            faces.Add(new Face(new List<int> { 1, 2, 5 }));

            return new Polyhedron(vertices, faces);
        }
        //тетраэдр
        public Polyhedron CreateTetrahedron()
        {
            List<Point3D> vertices = new List<Point3D>();
            List<Face> faces = new List<Face>();


            double halfSide = 100.0;
            double offsetX = pictureBox1.Width / 2;
            double offsetY = pictureBox1.Height / 2;

            vertices.Add(new Point3D(halfSide + offsetX, halfSide + offsetY, halfSide));
            vertices.Add(new Point3D(-halfSide + offsetX, -halfSide + offsetY, halfSide));
            vertices.Add(new Point3D(halfSide + offsetX, -halfSide + offsetY, -halfSide));
            vertices.Add(new Point3D(-halfSide + offsetX, halfSide + offsetY, -halfSide));


            faces.Add(new Face(new List<int> { 0, 1, 2 }));
            faces.Add(new Face(new List<int> { 0, 2, 3 }));
            faces.Add(new Face(new List<int> { 0, 3, 1 }));
            faces.Add(new Face(new List<int> { 1, 2, 3 }));

            return new Polyhedron(vertices, faces);
        }


        //тело вращения
        public Polyhedron CreateRevolvedShape(List<Point3D> points, char ax, int cnt)
        {
            double angleStep = 360.0 / cnt; // угол на каждый шаг разбиения
            double radiansStep = angleStep * Math.PI / 180; // угол в радианах

            List<Point3D> vertices = new List<Point3D>();
            List<Face> faces = new List<Face>();

            // Добавляем начальные точки образующей в вершины
            for (int i = 0; i < cnt; i++)
            {
                foreach (var p in points)
                {
                    // Рассчитываем вращение в зависимости от оси
                    double x = p.X, y = p.Y, z = p.Z;
                    double angle = radiansStep * i;

                    if (ax == 'x')
                    {
                        // Вращение вокруг оси X
                        y = p.Y * Math.Cos(angle) - p.Z * Math.Sin(angle);
                        z = p.Y * Math.Sin(angle) + p.Z * Math.Cos(angle);
                    }
                    else if (ax == 'y')
                    {
                        // Вращение вокруг оси Y
                        x = p.X * Math.Cos(angle) + p.Z * Math.Sin(angle);
                        z = -p.X * Math.Sin(angle) + p.Z * Math.Cos(angle);
                    }
                    vertices.Add(new Point3D(x, y, z));
                }
            }

            // Генерация боковых граней, соединяющих точки
            int profileSize = points.Count;
            for (int i = 0; i < cnt; i++)
            {
                for (int j = 0; j < profileSize; j++)
                {
                    int current = i * profileSize + j;
                    int current1 = i * profileSize + (j + 1) % profileSize;
                    int next = ((i + 1) % cnt) * profileSize + j;
                    int next1 = ((i + 1) % cnt) * profileSize + (j + 1) % profileSize;

                    // Определяем индексы четырёх углов каждой боковой грани
                    List<int> faceVertices = new List<int>
                    {
                        current,
                        current1,
                        next1,
                        next
                    };
                    faces.Add(new Face(faceVertices));
                }
            }

            double offsetX = pictureBox1.Width / 2;
            double offsetY = pictureBox1.Height / 2;
            for (int i = 0; i < vertices.Count; i++)
            {
                vertices[i] = new Point3D(vertices[i].X + offsetX, vertices[i].Y + offsetY, vertices[i].Z);
            }

            return new Polyhedron(vertices, faces);
        }



        // Отрисовка многогранников
        // =========================================================================
        public PointF Project(Point3D point, TransformationMatrix projectionMatrix)
        {

            Point3D projectedPoint = projectionMatrix.TransformForPerspect(point);
            return new PointF((float)projectedPoint.X, (float)projectedPoint.Y);

        }

        public PointF ProjectLinePoint(Point3D point, TransformationMatrix projectionMatrix)
        {
            Point3D projectedPoint = projectionMatrix.Transform(point);
            return new PointF((float)projectedPoint.X, (float)projectedPoint.Y);
        }


        //public void DrawPolyhedron(Polyhedron polyhedron, Graphics g, TransformationMatrix projectionMatrix)
        //{
        //    Pen pen = new Pen(Color.Black, 1);
        //    // Чтение значений из текстовых полей
        //    double x = double.Parse(x_view.Text);
        //    double y = double.Parse(y_view.Text);
        //    double z = double.Parse(z_view.Text);
        //    // Создание вектора обзора на основе введённых значений
        //    //Point3D viewVector = (new Point3D(x, y, z)).Normalize();
        //    var rotation_matr= TransformationMatrix.CreateRotationMatrix(x,y,z);
        //    //TransformPolyhedron(rotation_matr);

        //    polyhedron.CalculateNormals();
        //    //Point3D viewVector = new Point3D(0, 0, 1);
        //    Point3D viewVector = currentProjectionMatrix.Transform(new Point3D(0,0,1)).Normalize();
        //    foreach (Face face in polyhedron.Faces)
        //    {
        //        //var normal = roration_matr.Transform((face.Normal)).Normalize();
        //        //double dotProduct = Point3D.DotProduct(viewVector, currentProjectionMatrix.Transform(face.Normal).Normalize());
        //        //double dotProduct = Point3D.DotProduct(viewVector, currentProjectionMatrix.Transform(normal).Normalize());
        //        double dotProduct = Point3D.DotProduct(viewVector, currentProjectionMatrix.Transform(rotation_matr.Transform(face.Normal).Normalize()).Normalize());
        //        //double dotProduct = Point3D.DotProduct(viewVector, rotation_matr.Transform( currentProjectionMatrix.Transform(face.Normal).Normalize()).Normalize());
        //        if (dotProduct < 0)
        //        {
        //            for (int i = 0; i < face.Vertices.Count; i++)
        //            {
        //                int start = face.Vertices[i];
        //                int end = face.Vertices[(i + 1) % face.Vertices.Count];
        //                var rotation = TransformationMatrix.CreateRotationAroundAxis(x, y, z, GetPolyhedronCenter(polyhedron));
        //                //PointF p1 = Project(rotation.Transform(polyhedron.Vertices[start]), projectionMatrix);
        //                //PointF p2 = Project(rotation.Transform(polyhedron.Vertices[end]), projectionMatrix);
        //                PointF p1 = Project(polyhedron.Vertices[start], projectionMatrix);
        //                PointF p2 = Project(polyhedron.Vertices[end], projectionMatrix);

        //                g.DrawLine(pen, p1, p2);
        //            }
        //        }
        //    }
        //}




        public void DrawStraightLine(Polyhedron polyhedron, Graphics g, TransformationMatrix projectionMatrix)
        {
            Pen pen = new Pen(Color.Red, 2);

            PointF p1 = ProjectLinePoint(line[0], projectionMatrix);
            PointF p2 = ProjectLinePoint(line[1], projectionMatrix);

            g.DrawLine(pen, p1, p2);
        }



        // Обработчик события Paint для отрисовки на pictureBox1
        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            if (IsPaint)
            {
                if (polyhedron != null)
                {
                    DrawPolyhedron(polyhedron, e.Graphics, camera, currentProjectionMatrix);
                    if (line.Count == 2)
                        DrawStraightLine(polyhedron, e.Graphics, currentProjectionMatrix);
                }
                IsPaint = false;
            }
            else
                IsPaint = true;
        }


        // Применяем преобразования к многограннику
        // =========================================================================
        public static Point3D GetPolyhedronCenter(Polyhedron p)
        {
            if (p.Vertices == null || p.Vertices.Count == 0)
                return new Point3D(0, 0, 0);

            double sumX = 0, sumY = 0, sumZ = 0;

            foreach (var vertex in p.Vertices)
            {
                sumX += vertex.X;
                sumY += vertex.Y;
                sumZ += vertex.Z;
            }

            int numVertices = p.Vertices.Count;

            double centroidX = sumX / numVertices;
            double centroidY = sumY / numVertices;
            double centroidZ = sumZ / numVertices;

            return new Point3D(centroidX, centroidY, centroidZ);
        }


        private bool TransformPolyhedron(TransformationMatrix transformationMatrix)
        {
            if (polyhedron != null)
            {
                for (int i = 0; i < polyhedron.Vertices.Count; i++)
                {
                    polyhedron.Vertices[i] = transformationMatrix.Transform(polyhedron.Vertices[i]);
                }
                pictureBox1.Invalidate();
                return true;
            }
            return false;
        }
        private bool TransformPolyhedronNew(Polyhedron newpolyhedron, TransformationMatrix transformationMatrix)
        {
            if (newpolyhedron != null)
            {
                for (int i = 0; i < newpolyhedron.Vertices.Count; i++)
                {
                    newpolyhedron.Vertices[i] = transformationMatrix.Transform(newpolyhedron.Vertices[i]);
                }
                //pictureBox1.Invalidate();
                return true;
            }
            return false;
        }

        private void ApplyTranslation(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                if (double.TryParse(txtOffsetX.Text, out double dx) && double.TryParse(txtOffsetY.Text, out double dy)
                && double.TryParse(txtOffsetZ.Text, out double dz))
                {
                    TransformationMatrix matrix = TransformationMatrix.CreateTranslationMatrix(dx, dy, dz);
                    TransformPolyhedron(matrix);
                }
            }
        }

        private void ApplyRotation(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                if (double.TryParse(txtRotationX.Text, out double angleX) && double.TryParse(txtRotationY.Text, out double angleY)
                    && double.TryParse(txtRotationZ.Text, out double angleZ))
                {
                    Point3D center = GetPolyhedronCenter(polyhedron);
                    TransformationMatrix matrix = TransformationMatrix.CreateRotationAroundAxis(angleX, angleY, angleZ, center);
                    TransformPolyhedron(matrix);
                }
            }
        }

        private void ApplyScaling(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                if (double.TryParse(txtScaleX.Text, out double scaleFactorX) && double.TryParse(txtScaleY.Text, out double scaleFactorY)
                    && double.TryParse(txtScaleZ.Text, out double scaleFactorZ))
                {
                    Point3D center = GetPolyhedronCenter(polyhedron);
                    TransformationMatrix matrix = TransformationMatrix.CreateScalingMatrix(scaleFactorX, scaleFactorY, scaleFactorZ, center);
                    TransformPolyhedron(matrix);
                }
            }
        }

        private void cbFlipXY_CheckedChanged(object sender, EventArgs e)
        {
            Point3D center = GetPolyhedronCenter(polyhedron);
            TransformationMatrix matrix = TransformationMatrix.CreateReflectionMatrixXY(center);
            TransformPolyhedron(matrix);
        }

        private void cbFlipXZ_CheckedChanged(object sender, EventArgs e)
        {
            Point3D center = GetPolyhedronCenter(polyhedron);
            TransformationMatrix matrix = TransformationMatrix.CreateReflectionMatrixXZ(center);
            TransformPolyhedron(matrix);
        }

        private void cbFlipYZ_CheckedChanged(object sender, EventArgs e)
        {
            Point3D center = GetPolyhedronCenter(polyhedron);
            TransformationMatrix matrix = TransformationMatrix.CreateReflectionMatrixYZ(center);
            TransformPolyhedron(matrix);
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            zBuffer = new double[pictureBox1.Width, pictureBox1.Height];
            pictureBox1.Invalidate();
        }


        // Поворот вокруг прямой на заданный угол
        // =========================================================================
        private void pictureBox1_MouseClick(object sender, MouseEventArgs e)
        {
            if (line.Count == 2)
                line.Clear();

            int x = e.Location.X;
            int y = e.Location.Y;
            line.Add(new Point3D(x, y, 0));
            if (line.Count == 1)
            {
                firstPointX.Text = x.ToString();
                firstPointY.Text = y.ToString();
                firstPointZ.Text = "0";

                secondPointX.Text = "0";
                secondPointY.Text = "0";
                secondPointZ.Text = "0";
            }
            else if (line.Count == 2)
            {
                secondPointX.Text = x.ToString();
                secondPointY.Text = y.ToString();
                secondPointZ.Text = "0";
                pictureBox1.Invalidate();
            }
        }


        private void ChangeFirstPoint(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                if (double.TryParse(firstPointX.Text, out double x) && double.TryParse(firstPointY.Text, out double y)
                    && double.TryParse(firstPointZ.Text, out double z))
                {
                    line[0] = new Point3D(x, y, z);
                    pictureBox1.Invalidate();
                }
            }
        }

        private void ChangeSecondPoint(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                if (double.TryParse(secondPointX.Text, out double x) && double.TryParse(secondPointY.Text, out double y)
                    && double.TryParse(secondPointZ.Text, out double z))
                {
                    line[1] = new Point3D(x, y, z);
                    pictureBox1.Invalidate();
                }
            }
        }


        private void ChangeAngle(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                if (double.TryParse(txtAngle.Text, out double angle) &&
                    line.Count == 2)
                {
                    TransformationMatrix matrix = TransformationMatrix.CreateRotationAroundLine(line[0], line[1], angle);
                    TransformPolyhedron(matrix);
                }
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (comboBox1.SelectedItem.ToString())
            {
                case "Центральная":
                    currentProjectionMatrix = TransformationMatrix.PerspectiveProjection(5000);
                    IsPersp = true;
                    break;
                case "Аксонометрическая":
                    currentProjectionMatrix = TransformationMatrix.AxonometricProjection(45, 30);
                    IsPersp = false;
                    break;
            }
            pictureBox1.Invalidate();
        }

        private void cnt_points_TextChanged(object sender, EventArgs e)
        {
            // Очищаем предыдущие элементы, если они есть
            panel_points.Controls.Clear();
            panel_points.AutoScroll = true; // Включаем прокрутку для панели

            // Пытаемся преобразовать значение из textBoxMain в число
            if (int.TryParse(cnt_points.Text, out int rowCount) && rowCount > 0)
            {
                int spacing = 50; // расстояние между рядами
                int maxRows = panel_points.Height / spacing; // максимальное количество рядов без прокрутки

                for (int i = 0; i < rowCount; i++)
                {
                    // Создаем метку "X"
                    Label labelX = new Label();
                    labelX.Text = $"X{i + 1}";
                    labelX.Location = new Point(10, i * spacing);
                    labelX.AutoSize = true;

                    // Создаем TextBox для "X"
                    System.Windows.Forms.TextBox textBoxX = new System.Windows.Forms.TextBox();
                    textBoxX.Location = new Point(60, i * spacing);
                    textBoxX.Width = 70;

                    // Создаем метку "Y"
                    Label labelY = new Label();
                    labelY.Text = $"Y{i + 1}";
                    labelY.Location = new Point(130, i * spacing);
                    labelY.AutoSize = true;

                    // Создаем TextBox для "Y"
                    System.Windows.Forms.TextBox textBoxY = new System.Windows.Forms.TextBox();
                    textBoxY.Location = new Point(180, i * spacing);
                    textBoxY.Width = 70;

                    // Добавляем все элементы в панель
                    panel_points.Controls.Add(labelX);
                    panel_points.Controls.Add(textBoxX);
                    panel_points.Controls.Add(labelY);
                    panel_points.Controls.Add(textBoxY);
                }

                // Устанавливаем размер AutoScrollMinSize, чтобы задать максимальную прокрутку по вертикали
                panel_points.AutoScrollMinSize = new Size(0, rowCount * spacing);
            }
            else
            {
                // Если введено некорректное значение, очищаем панель
                panel_points.Controls.Clear();
            }
        }

        private void panel2_Paint(object sender, PaintEventArgs e)
        {

        }

        private void create_fig_Click(object sender, EventArgs e)
        {
            List<Point3D> points = new List<Point3D>();

            for (int i = 0; i < panel_points.Controls.Count; i += 4)
            {
                // Получаем TextBox для X и Y координат в текущем ряду
                if (panel_points.Controls[i + 1] is System.Windows.Forms.TextBox textBoxX &&
                    panel_points.Controls[i + 3] is System.Windows.Forms.TextBox textBoxY)
                {
                    // Считываем значения из TextBox, если они корректны
                    if (double.TryParse(textBoxX.Text, out double x) &&
                        double.TryParse(textBoxY.Text, out double y))
                    {
                        // Добавляем точку с Z = 0 в список
                        points.Add(new Point3D(x, y, 0));
                    }
                    else
                    {
                        MessageBox.Show("Введите корректные числовые значения для всех точек.");
                        return;
                    }
                }
            }
            char ax = 'y';
            if (cbY.Checked) ax = 'y';
            else if (cbX.Checked) ax = 'x';
            int cnt;
            int.TryParse(tbCnt.Text, out cnt);
            polyhedron = CreateRevolvedShape(points, ax, cnt);
            pictureBox1.Invalidate();
        }

        private void фигураВращенияToolStripMenuItem_Click(object sender, EventArgs e)
        {
            panel3.Enabled = panel3.Visible = false;
            panel2.Enabled = panel2.Visible = true;
            polyhedron = new Polyhedron();
            pictureBox1.Invalidate();

        }

        private void икосаэдрToolStripMenuItem_Click(object sender, EventArgs e)
        {
            panel3.Enabled = panel3.Visible = false;
            panel2.Enabled = panel2.Visible = false;
            polyhedron = CreateIcosahedron();
            pictureBox1.Invalidate();
        }

        private void кубToolStripMenuItem_Click(object sender, EventArgs e)
        {
            panel3.Enabled = panel3.Visible = false;
            panel2.Enabled = panel2.Visible = false;
            polyhedron = CreateCube();
            pictureBox1.Invalidate();
        }

        private void додекаэдрToolStripMenuItem_Click(object sender, EventArgs e)
        {
            panel3.Enabled = panel3.Visible = false;
            panel2.Enabled = panel2.Visible = false;
            polyhedron = CreateDodecahedron();
            pictureBox1.Invalidate();
        }

        private void октаэдрToolStripMenuItem_Click(object sender, EventArgs e)
        {
            panel3.Enabled = panel3.Visible = false;
            panel2.Enabled = panel2.Visible = false;
            polyhedron = CreateOctahedron();
            pictureBox1.Invalidate();
        }

        private void тетраэдрToolStripMenuItem_Click(object sender, EventArgs e)
        {
            panel3.Enabled = panel3.Visible = false;
            panel2.Enabled = panel2.Visible = false;
            polyhedron = CreateTetrahedron();
            pictureBox1.Invalidate();
        }
        // Сохранение и загрузка в файл
        // =========================================================================
        // Сохраняем в файл
        public void SaveToOBJ(string filePath)
        {
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                // Сохраняем вершины
                foreach (var vertex in polyhedron.Vertices)
                {
                    writer.WriteLine($"v {vertex.X} {vertex.Y} {vertex.Z}");
                }

                // Сохраняем грани
                foreach (var face in polyhedron.Faces)
                {
                    string faceLine = "f";
                    foreach (var vertexIndex in face.Vertices)
                    {
                        faceLine += $" {vertexIndex + 1}"; // +1, так как индексация в OBJ начинается с 1
                    }
                    writer.WriteLine(faceLine);
                }
            }
        }

        // Загружаем из файла
        public void LoadFromOBJ(string filePath)
        {
            var vertices = new List<Point3D>();
            var faces = new List<Face>();

            foreach (string line in File.ReadLines(filePath))
            {
                if (line.StartsWith("v "))
                {
                    var parts = line.Split(' ');
                    double x = double.Parse(parts[1]);
                    double y = double.Parse(parts[2]);
                    double z = double.Parse(parts[3]);
                    vertices.Add(new Point3D(x, y, z));
                }
                else if (line.StartsWith("f "))
                {
                    var parts = line.Split(' ');
                    var vertexIndices = new List<int>();

                    for (int i = 1; i < parts.Length; i++)
                    {
                        int vertexIndex = int.Parse(parts[i].Split('/')[0]) - 1;
                        vertexIndices.Add(vertexIndex);
                    }
                    faces.Add(new Face(vertexIndices));
                }
            }

            polyhedron = new Polyhedron(vertices, faces);
        }

        private void saveTool_Click(object sender, EventArgs e)
        {
            if (polyhedron == null)
                return;

            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "OBJ files (*.obj)|*.obj|All files (*.*)|*.*";
                saveFileDialog.Title = "Сохранить файл как";

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = saveFileDialog.FileName;
                    SaveToOBJ(filePath);
                }
            }
        }

        private void openTool_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "OBJ files (*.obj)|*.obj|All files (*.*)|*.*";
                openFileDialog.Title = "Открыть файл";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = openFileDialog.FileName;
                    LoadFromOBJ(filePath);
                    pictureBox1.Invalidate();
                }
            }
        }

        private void графикToolStripMenuItem_Click(object sender, EventArgs e)
        {
            panel3.Enabled = panel3.Visible = true;
            panel2.Enabled = panel2.Visible = false;
            polyhedron = new Polyhedron();
            pictureBox1.Invalidate();
        }

        private Polyhedron CreatePolyhedron(int gridSize, float x0, float y0, float x1, float y1, Func<double, double, double> func)
        {
            List<Point3D> vertices = new List<Point3D>();
            List<Face> faces = new List<Face>();

            float dx = (x1 - x0) / (gridSize);
            float dy = (y1 - y0) / (gridSize);
            double offsetX = pictureBox1.Width / 2;
            double offsetY = pictureBox1.Height / 2;
            float z = 0;
            // вершины
            for (int i = 0; i < gridSize; i++)
            {
                for (int j = 0; j < gridSize; j++)
                {
                    float x = (x0 + i * dx);
                    float y = (y0 + j * dy);

                    if (funcomboBox.SelectedItem.ToString() == "x^2+y^2")
                    {
                        z = ((float)func(x, y)) * 5;
                    }
                    else
                    {
                        z = ((float)func(x, y)) * 20;
                    }
                    x *= 20;
                    y *= 20;
                    vertices.Add(new Point3D(x + offsetX, y + offsetY, z));
                }
            }

            //грани
            for (int i = 0; i < gridSize - 1; i++)
            {
                for (int j = 0; j < gridSize - 1; j++)
                {
                    int topLeft = i * gridSize + j;
                    int topRight = i * gridSize + (j + 1);
                    int bottomLeft = (i + 1) * gridSize + j;
                    int bottomRight = (i + 1) * gridSize + (j + 1);


                    //faces.Add(new Face(new List<int> { topLeft, topRight, bottomLeft }));
                    //faces.Add(new Face(new List<int> { topRight, bottomRight, bottomLeft }));
                    faces.Add(new Face(new List<int> { topLeft, topRight, bottomRight, bottomLeft }));
                }
            }

            return new Polyhedron(vertices, faces);
        }
        private void button1_Click(object sender, EventArgs e)
        {
            int x0;
            int.TryParse(textBox1.Text, out x0);
            int x1;
            int.TryParse(textBox2.Text, out x1);
            int y0;
            int.TryParse(textBox3.Text, out y0);
            int y1;
            int.TryParse(textBox4.Text, out y1);
            int gridSize;
            int.TryParse(textBox5.Text, out gridSize);
            gridSize += 1;


            switch (funcomboBox.SelectedItem.ToString())
            {
                case "sinX + cosY":

                    polyhedron = CreatePolyhedron(gridSize, x0, y0, x1, y1, (a, b) => (float)(Math.Sin(a) + Math.Cos(b)));
                    break;
                case "5*(Cos(r)/r+0.1), r=x^2+y^2+1":
                    polyhedron = CreatePolyhedron(gridSize, x0, y0, x1, y1, (a, b) => (float)(5 * (Math.Cos(a * a + b * b + 1) / (a * a + b * b + 1) + 0.1)));
                    break;
                case "x^2+y^2":
                    polyhedron = CreatePolyhedron(gridSize, x0, y0, x1, y1, (a, b) => (float)(a * a + b * b));
                    break;
                case "Cos(r)/(r+1), r=x^2+y^2":
                    polyhedron = CreatePolyhedron(gridSize, x0, y0, x1, y1, (a, b) => (float)(Math.Cos(a * a + b * b) / (a * a + b * b + 1)));
                    break;
                case "Sin(x)*Cos(y);":
                    polyhedron = CreatePolyhedron(gridSize, x0, y0, x1, y1, (a, b) => (float)(Math.Sin(a) * Math.Cos(b)));
                    break;
            }
            pictureBox1.Invalidate();
        }

        public class Camera
        {
            public Point3D Position { get; set; }
            public double Pitch { get; set; }
            public double Yaw { get; set; }
            public double Roll { get; set; }

            public Camera()
            {
                //Position = new Point3D(100, 300, -400);
                Position = new Point3D(0, 0, -400);

                Pitch = 10;
                Yaw = 10;
                Roll = 0;
            }


            public Camera(Point3D position, double pitch, double yaw, double roll)
            {
                Position = position;
                Pitch = pitch;
                Yaw = yaw;
                Roll = roll;
            }


            public Point3D GetDirection(Polyhedron polyhedron)
            {
                return new Point3D(0, 0, 1); 
            }


            public TransformationMatrix GetViewMatrix()//из мировых в коорд камеры
            {
                var translationMatrix = TransformationMatrix.CreateTranslationMatrix(-Position.X, -Position.Y, -Position.Z);//матр трансляции
                var rotationMatrix = TransformationMatrix.CreateReverseRotationMatrix(-Pitch, -Yaw, -Roll);//матр вращения
                return TransformationMatrix.Multiply(translationMatrix, rotationMatrix);
            }
        }

        // Отрисовка многогранника
        // =================================================================
        public void DrawPolyhedron(Polyhedron polyhedron, Graphics g, Camera camera, TransformationMatrix projectionMatrix)
        {
            Polyhedron copy_pol = new Polyhedron(new List<Point3D>(polyhedron.Vertices), new List<Face>(polyhedron.Faces));
            projectionMatrix = TransformationMatrix.PerspectiveProjection(500);
            Pen pen = new Pen(Color.Black, 1);

            
            var viewMatrix = camera.GetViewMatrix();

            TransformPolyhedronNew(copy_pol, viewMatrix);

            copy_pol.CalculateNormals();

            // Направление камеры для отсечения нелицевых граней
            Point3D viewVector = camera.GetDirection(polyhedron);

            for (int x = 0; x < pictureBox1.Width; x++)
                for (int y = 0; y < pictureBox1.Height; y++)
                    zBuffer[x, y] = double.MaxValue;

            Point[] trianglePoints = new Point[3];
            Point3D[] triangleVertices = new Point3D[3];
            Bitmap bmp = new Bitmap(pictureBox1.Width, pictureBox1.Height);

            foreach (var face in copy_pol.Faces)
            {
                double dotProduct = Point3D.DotProduct(face.Normal, viewVector);

                if (dotProduct < 0) 
                {
                    for (int i = 1; i < face.Vertices.Count-1; i++)
                    {
                        triangleVertices[0] = copy_pol.Vertices[face.Vertices[0]];
                        triangleVertices[1] = copy_pol.Vertices[face.Vertices[i]];
                        triangleVertices[2] = copy_pol.Vertices[face.Vertices[i + 1]];

                        trianglePoints = triangleVertices
                            .Select(vertex => Project(vertex, projectionMatrix))
                            .Select(p => new Point((int)p.X, (int)p.Y))
                            .ToArray();

                        RasterizeTriangle(g, trianglePoints, triangleVertices, face.FaceColor, bmp);
                    }
                }
            }

            g.DrawImage(bmp, 0, 0);
        }


        // Растеризация треугольника
        // =================================================================
        public class PointComparer : IComparer<Point>
        {
            public int Compare(Point p1, Point p2)
            {
                return p1.Y.CompareTo(p2.Y);
            }
        }

        private void RasterizeTriangle(Graphics g, Point[] trianglePoints, Point3D[] triangleVertices, Color color, Bitmap bmp)
        {
            Array.Sort(trianglePoints, triangleVertices, new PointComparer());

            int minY = Math.Max(0, trianglePoints[0].Y);
            int maxY = Math.Min(trianglePoints[2].Y, pictureBox1.Height - 1);

            using (var fastBitmap = new FastBitmap.FastBitmap(bmp))
            {
                for (int y = minY; y <= maxY; y++)
                {
                    int xLeft = int.MaxValue;
                    int xRight = int.MinValue;
                    double zLeft = 0;
                    double zRight = 0;

                    for (int i = 0; i < 3; i++)
                    {
                        Point p1 = trianglePoints[i];
                        Point p2 = trianglePoints[(i + 1) % 3];

                        Point3D v1 = triangleVertices[i];
                        Point3D v2 = triangleVertices[(i + 1) % 3];

                        if ((p1.Y <= y && p2.Y > y) || (p2.Y <= y && p1.Y > y))
                        {
                            float t = (float)(y - p1.Y) / (p2.Y - p1.Y);
                            int x = (int)(p1.X + t * (p2.X - p1.X));
                            double z = v1.Z + t * (v2.Z - v1.Z);

                            if (x < xLeft || (x == xLeft && z < zLeft))
                            {
                                xLeft = x;
                                zLeft = z;
                            }
                            if (x > xRight || (x == xRight && z < zRight))
                            {
                                xRight = x;
                                zRight = z;
                            }
                        }
                    }

                    if (xLeft < xRight)
                    {
                        double zDelta = (zRight - zLeft) / (xRight - xLeft);
                        double currentZ = zLeft;

                        for (int x = xLeft; x <= xRight; x++, currentZ += zDelta)
                        {
                            if (x >= 0 && x < pictureBox1.Width && y >= 0 && y < pictureBox1.Height)
                            {
                                if (currentZ < zBuffer[x, y])
                                {
                                    zBuffer[x, y] = currentZ;
                                    fastBitmap[x, y] = color;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void ChangeCameraAngle(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                if (double.TryParse(cameraX.Text, out double p) &&
                    double.TryParse(cameraY.Text, out double y) &&
                    double.TryParse(cameraZ.Text, out double r))
                {
                    camera.Pitch = p;
                    camera.Yaw = y;
                    camera.Roll = r;
                    pictureBox1.Invalidate();
                }
            }
        }

        private void ChangeCameraPosition(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                if (int.TryParse(cameraX0.Text, out int x0) &&
                    (int.TryParse(cameraY0.Text, out int y0)))
                {
                    camera.Position = new Point3D(x0, y0, camera.Position.Z);
                }
            }
        }

        private void CameraRotation(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                if (double.TryParse(camRot.Text, out double rot))
                {
                    Point3D target = GetPolyhedronCenter(polyhedron);
                    Point3D direction = Point3D.Vector(target, camera.Position);
                    TransformationMatrix rotationMatrix = TransformationMatrix.CreateRotationMatrixY(rot);
                    Point3D rotatedDirection = rotationMatrix.Transform(direction);

                    camera.Position = new Point3D(
                        target.X + rotatedDirection.X,
                        target.Y + rotatedDirection.Y,
                        target.Z + rotatedDirection.Z
                    );

                    camera.Yaw += rot;

                    pictureBox1.Invalidate();
                }
            }
        }

        public class Light
        {
            public Point3D Position { get; set; }
            public Color Color { get; set; }
            public Light(Point3D position, Color color)
            {
                Position = position;
                Color = color;
            }

        }
    }
}
