# 为Cursor准备的README文档
此项目为Unity Project，版本号6.3.9f1
# 代码原则
数据驱动开发原则：尽量将代码的逻辑和数据分离
不需要生成.meta file，unity editor会自动生成。

易读性原则：将代码的不同功能模块用region区分开来，例子有：
1. Editor (Serialized)
2. Private Variables
2. Events
3. Public Functions
4. Private Functions
5. Helper Functions