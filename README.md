# Распространение лесных пожаров на мезомасштабном уровне с учётом физики растительности в режиме реального времени
Данная работа представляет собой моделирование распространения лесных пожаров в трёхмерном пространстве. Основной особенностью является возможность моделирования физики растительности в реальном времени для множества детализированных деревьев в масштабе леса. Достигнуто это было благодаря модификации мезомасштабного представления растительности за счёт введения физического представления модулей и связей между ними. 

Проект является частью магистерской выпускной квалификационной работы образовательной программы "Технологии разработки компьютерных игр" [школы разработки видеоигр Университета ИТМО](https://itmo.games/). Для реализации разработанной модели был выбран инструментарий игрового движка Unity.

## Обзор
На изображении ниже представлены три основных этапа генерации экземпляров моделей растительности на основе модификации мезомасштабного подхода: формирование графового представления структуры дерева, апроксимация исходной геометрии дерева, генерация модулей и их физических оболочек.

![Wildfire result screenshot](Docs/TreeGeneratorSteps.PNG)

На изображении ниже представлен один из возможных сценариев распространения лесного пожара. В качестве инициатора пожара выступает факел. Точкой старта пожара является правый нижний угол сцены. Скорость и направление ветра принимаются 5 метров в секунду и южное соответственно. Сцена имеет размеры 40 x 16 x 40 метров. Разрешение сетки 80 x 32 x 80 метров. В сцене присутствует 80 деревьев, каждое из которых состоит из 44 модулей. Каждый модуль находится под постоянным воздействием силы ветра и смещается по его направлению.

![Wildfire result screenshot](Docs/WildfireResultExample.png)

## Запуск и использование
Работа является проектом, разработанным в Unity. Для запуска необходимо скачать репозиторий и открыть его через Unity Hub. Собранные тестовые сцены находятся по пути **_Assets/Scenes/TreeScene.unity_** и **_Assets/Scenes/ForestScene.unity_** и содержат настройки из обзора.

### Генерация экземпляров модели растительности
Для того, чтобы приступить к создаю экземпляра модели дерева необходимо создать пустой игровой объект (GameObject) и прикрепить к нему компонент (Component) **Path Creator**. Данный компонент предназначен для формирования структуры дерева с помощью кривых Безье.
У данного компонента есть следующие важные настройки:
-  Control Mode - параметр, определяющий как точки на данной кривой Безье будут переходить друг в друга
-  Enable Transforms - включить/выключить отображение Gizmo
-  Reset Path - сбросить траекторию кривой Безье к заводскому положению

![Wildfire result screenshot](Docs/WildfireResultExample.png)



