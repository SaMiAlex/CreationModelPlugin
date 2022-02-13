using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreationModelPlugin
{
    [TransactionAttribute(TransactionMode.Manual)]
    public class CreationModel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            List<Level> levels = LevelUtil.GetLevels(commandData);
            Level level1 = levels
                .Where(x => x.Name.Equals("Уровень 1"))
                .FirstOrDefault();
            Level level2 = levels
                .Where(x => x.Name.Equals("Уровень 2"))
                .FirstOrDefault();

            CreateWalls(doc, level1, level2);

            return Result.Succeeded;
        }

        private static void CreateWalls(Document doc, Level level1, Level level2)
        {
            double width = UnitUtils.ConvertToInternalUnits(10000, UnitTypeId.Millimeters);
            double depth = UnitUtils.ConvertToInternalUnits(5000, UnitTypeId.Millimeters);
            double dx = width / 2;
            double dy = depth / 2;

            List<XYZ> points = new List<XYZ>();
            points.Add(new XYZ(-dx, -dy, 0));
            points.Add(new XYZ(dx, -dy, 0));
            points.Add(new XYZ(dx, dy, 0));
            points.Add(new XYZ(-dx, dy, 0));
            points.Add(new XYZ(-dx, -dy, 0));

            List<Wall> walls = new List<Wall>();

            Transaction ts = new Transaction(doc, "Построение стен");
            ts.Start();
            for (int i = 0; i < 4; i++)
            {
                Line line = Line.CreateBound(points[i], points[i + 1]);
                Wall wall = Wall.Create(doc, line, level1.Id, false);
                walls.Add(wall);
                wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(level2.Id);
            }

            AddDoor(doc, level1, walls[0]);
            AddWindow(doc, level1, walls[1]);
            AddWindow(doc, level1, walls[2]);
            AddWindow(doc, level1, walls[3]);
            AddRoof(doc, level2, walls);

            ts.Commit();
        }

        private static void AddRoof(Document doc, Level level2, List<Wall> walls)
        {
            RoofType roofType = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofType))
                .OfType<RoofType>()
                .Where(x => x.Name.Equals("Типовой - 400мм"))
                .Where(x => x.FamilyName.Equals("Базовая крыша"))
                .FirstOrDefault();

            //получаем толщину найденной крыши
            Parameter roofHeight = roofType.LookupParameter("Толщина");
            double h = roofHeight.AsDouble();

            //получаем величину смещения для крайних точек крыши
            double wallWidth = walls[0].Width;
            double dt = wallWidth / 2;

            //получаем точки для проекции поперечной линии крыши
            LocationCurve curve1 = walls[1].Location as LocationCurve;
            XYZ pp1 = curve1.Curve.GetEndPoint(0);
            XYZ pp2 = curve1.Curve.GetEndPoint(1);

            //получаем точки для проекции продольной линии крыши
            LocationCurve curve2 = walls[0].Location as LocationCurve;
            XYZ pp3 = curve2.Curve.GetEndPoint(0);
            XYZ pp4 = curve2.Curve.GetEndPoint(1);

            //получаем координаты начала и конца выдавливания
            double startPointExtrusion = pp3.X - dt;
            double endPointExtrusion = pp4.X + dt;

            //получаем точки для поперечной линии крыши
            XYZ p1 = new XYZ(pp1.X, pp1.Y - dt, level2.Elevation + h);
            XYZ p2 = new XYZ(pp2.X, pp2.Y + dt, level2.Elevation + h);
            XYZ p3 = new XYZ(pp2.X, (pp1.Y + pp2.Y) / 2, level2.Elevation + h + 5);

            //строим поперечную линию крыши по полученным точкам
            CurveArray curveArray = new CurveArray();
            curveArray.Append(Line.CreateBound(p1, p3));
            curveArray.Append(Line.CreateBound(p3, p2));

            //создаем плоскость и крышу методом выдавливания по линии
            ReferencePlane plane = doc.Create.NewReferencePlane(new XYZ(0, 0, 0), new XYZ(0, 0, 1), new XYZ(0, 1, 0), doc.ActiveView);
            ExtrusionRoof roof = doc.Create.NewExtrusionRoof(curveArray, plane, level2, roofType, startPointExtrusion, endPointExtrusion);




            // метод построения крыши FootPrintRoof (по стенам)

            //double wallWidth = walls[0].Width;
            //double dt = wallWidth / 2;

            //List<XYZ> points = new List<XYZ>();
            //points.Add(new XYZ(-dt, -dt, 0));
            //points.Add(new XYZ(dt, -dt, 0));
            //points.Add(new XYZ(dt, dt, 0));
            //points.Add(new XYZ(-dt, dt, 0));
            //points.Add(new XYZ(-dt, -dt, 0));


            //Application application = doc.Application;
            //CurveArray footPrint = application.Create.NewCurveArray();

            //for (int i = 0; i < 4; i++)
            //{
            //    LocationCurve curve = walls[i].Location as LocationCurve;
            //    XYZ p1 = curve.Curve.GetEndPoint(0);
            //    XYZ p2 = curve.Curve.GetEndPoint(1);
            //    Line line = Line.CreateBound(p1 + points[i], p2 + points[i+1]);
            //    footPrint.Append(line);
            //}

            //ModelCurveArray modelCurveArray = new ModelCurveArray();
            //FootPrintRoof footPrintRoof = doc.Create.NewFootPrintRoof(footPrint, level2, roofType, out modelCurveArray);

            //задание характеристик через итератор
            //ModelCurveArrayIterator iterator = modelCurveArray.ForwardIterator(); 
            //iterator.Reset();
            //while (iterator.MoveNext())
            //{
            //    ModelCurve modelCurve = iterator.Current as ModelCurve;
            //    footPrintRoof.set_DefinesSlope(modelCurve, true);
            //    footPrintRoof.set_SlopeAngle(modelCurve, 0.5);
            //}

            //задание характеристик крыши в цикле
            //foreach (ModelCurve m in modelCurveArray)
            //{
            //    footPrintRoof.set_DefinesSlope(m, true);
            //    footPrintRoof.set_SlopeAngle(m, 0.5);
            //}


        }

        private static void AddWindow(Document doc, Level level1, Wall wall)
        {
            FamilySymbol windowType = new FilteredElementCollector(doc)
                 .OfClass(typeof(FamilySymbol))
                 .OfCategory(BuiltInCategory.OST_Windows)
                 .OfType<FamilySymbol>()
                 .Where(x => x.Name.Equals("0915 x 1220 мм"))
                 .Where(x => x.FamilyName.Equals("Фиксированные"))
                 .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point1 + point2) / 2;
            XYZ location = new XYZ(point.X, point.Y, 2.6246);


            if (!windowType.IsActive)
                windowType.Activate();

            FamilyInstance window = doc.Create.NewFamilyInstance(location, windowType, wall, level1, StructuralType.NonStructural);
            //Parameter elevation = window.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM);  


        }

        private static void AddDoor(Document doc, Level level1, Wall wall)
        {
            FamilySymbol doorType = new FilteredElementCollector(doc)
                 .OfClass(typeof(FamilySymbol))
                 .OfCategory(BuiltInCategory.OST_Doors)
                 .OfType<FamilySymbol>()
                 .Where(x => x.Name.Equals("0915 x 2134 мм"))
                 .Where(x => x.FamilyName.Equals("Одиночные-Щитовые"))
                 .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point1 + point2) / 2;
            if (!doorType.IsActive)
                doorType.Activate();
            doc.Create.NewFamilyInstance(point, doorType, wall, level1, StructuralType.NonStructural);
        }
    }
}
