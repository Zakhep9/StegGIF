using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections;

namespace StegGIF
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        static class Constants
        {
            public const byte EXTENSION_INTRODUCER = 0x21;
            public const byte IMAGE_DESCRIPTOR = 0x2C;
            public const byte TRAILER = 0x3B;

            public const byte GRAPHIC_CONTROL = 0xF9;
            public const byte APPLICATION_EXTENSION = 0xFF;
            public const byte COMMENT_EXTENSION = 0xFE;
            public const byte PLAINTEXT_EXTENSION = 0x01;
        }

        struct screen_descriptor_t
        {
            public ushort width, height;
            public byte fields, background_color, ratio;
        }

        struct rgb
        {
            public byte R, G, B;
        }

        struct image_descriptor_t
        {
            public ushort left_position, top_position,
                width, height;
            public byte fields;
        }

        struct dictionary_entry_t
        {
            public byte b;
            public int prev;
            public int len;
        }

        struct extension_t
        {
            public byte extension_code, block_size;
        }

        struct graphic_control_extension_t
        {
            public byte fields, transparent_color_index;
            public ushort delay_time;
        }

        struct application_extension_t
        {
            public string application_id;
            public string version;
        }

        struct plaintext_extension_t
        {
            public ushort left, top, width, height;
            public byte cell_width, cell_height, foreground_color, background_color;
        }

        static FileStream OriginFile = null;
        static BinaryReader OriginBR = null;

        static FileStream MessageFile = null;
        static BinaryReader MessageBR = null;

        static FileStream NewFile = null;
        static BinaryWriter NewBR = null;

        Image ActiveImage;
        Image NoImage;

        static bool show_bytes = true;

        static byte OriginByte;
        static byte FakeByte;

        static int count_of_pictures = 0;
        //процедура открытия изображения для встраивания информации
        private void button1_Click(object sender, EventArgs e)
        {
            pictureBox2.Image = NoImage;
            pictureBox1.Image = NoImage;
            if (ActiveImage != null)
                ActiveImage.Dispose();
            //gifimage.Dispose();

            OriginFile = null;
            //MessageFile = null;
            string FilePic;
            OpenFileDialog dPic = new OpenFileDialog();
            //допустимые расширения изображения
            dPic.Filter = "Файлы изображений (*.gif)|*.gif|Все файлы (*.*)|*.*";
            if (dPic.ShowDialog() == DialogResult.OK)
            {
                //если изображение успешно открыто => запоминаем путь к файлу
                FilePic = dPic.FileName;
            }
            else
            {
                //иначе выходим из метода
                FilePic = "";
                return;
            }
            //создаем поток для работы с файлами
            try
            {
                //открываем поток
                OriginFile = new FileStream(FilePic, FileMode.Open);
            }
            catch (IOException)
            {
                //в случае неудачи выводим сообщение об ошибке. Выходим из метода
                MessageBox.Show("Ошибка открытия файла", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            textBox1.Text = FilePic;
            OriginBR = new BinaryReader(OriginFile);

        }

        //процедура открытия текстового файла с встраиваемой информацией
        private void button2_Click(object sender, EventArgs e)
        {
            //OriginFile = null;
            MessageFile = null;
            string FileText;
            OpenFileDialog dText = new OpenFileDialog();
            //допустимые расширения текстового файла
            dText.Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*";
            if (dText.ShowDialog() == DialogResult.OK)
            {
                //если текстовый файл успешно открыт => запоминаем путь к файлу
                FileText = dText.FileName;
            }
            else
            {
                //иначе выходим из метода
                FileText = "";
                return;
            }
            //создаем поток для работы с файлами
            try
            {
                MessageFile = new FileStream(FileText, FileMode.Open); //открываем поток
            }
            catch (IOException)
            {
                //в случае неудачи выводим сообщение об ошибке. Выходим из метода
                MessageBox.Show("Ошибка открытия файла", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            textBox2.Text = FileText;
            MessageBR = new BinaryReader(MessageFile, Encoding.UTF8);
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            if(OriginBR != null)
                OriginBR.Close();
            if(OriginFile != null)
                OriginFile.Close();
            if(MessageBR != null)
                MessageBR.Close();
            if(MessageFile != null)
                MessageFile.Close();
            if (NewFile != null)
                NewFile.Close();
            if (NewBR != null)
                NewBR.Close();
        }

        //функция перевода одного байта в массив бит
        static private BitArray ByteToBit(byte src)
        {
            BitArray bitArray = new BitArray(8);
            bool st = false;
            for (int i = 0; i < 8; i++)
            {
                if ((src >> i & 1) == 1)
                {
                    st = true;
                }
                else st = false;
                bitArray[i] = st;
            }
            return bitArray;
        }

        //функция перевода массива бит в один байт
        static private byte BitToByte(BitArray scr)
        {
            byte num = 0;
            for (int i = 0; i < scr.Count; i++)
                if (scr[i] == true)
                    num += (byte)Math.Pow(2, i);
            return num;
        }

        //метод сокрытия сообщения в элементах палитры
        static private bool InCryptByteLSB(rgb[] list, byte message, int index)
        {
            //list - список всех элементов палитры
            //message - очередной байт сообщения
            //index - адрес элемента палитры, в который сообщение встраивается

            //массив бит для элемента палитры
            BitArray colorArray;
            //массив бит для очередного байта сообщения
            BitArray messageArray;
            messageArray = ByteToBit(message);

            //заменяем НЗБ красной компоненты на бит сообщения
            colorArray = ByteToBit(list[index].R);
            colorArray[0] = messageArray[0];
            list[index].R = BitToByte(colorArray);

            //заменяем НЗБ зеленой компоненты на бит сообщения
            colorArray = ByteToBit(list[index].G);
            colorArray[0] = messageArray[1];
            list[index].G = BitToByte(colorArray);

            //заменяем два НЗБ синей компоненты на два бита сообщения
            colorArray = ByteToBit(list[index].B);
            colorArray[0] = messageArray[2];
            colorArray[1] = messageArray[3];
            list[index].B = BitToByte(colorArray);

            index++;

            //заменяем НЗБ красной компоненты на бит сообщения
            colorArray = ByteToBit(list[index].R);
            colorArray[0] = messageArray[4];
            list[index].R = BitToByte(colorArray);

            //заменяем НЗБ зеленой компоненты на бит сообщения
            colorArray = ByteToBit(list[index].G);
            colorArray[0] = messageArray[5];
            list[index].G = BitToByte(colorArray);

            //заменяем два НЗБ синей компоненты на два бита сообщения
            colorArray = ByteToBit(list[index].B);
            colorArray[0] = messageArray[6];
            colorArray[1] = messageArray[7];
            list[index].B = BitToByte(colorArray);

            return true;
        }

        struct color_table_element_t
        {
            public byte color;
            public uint count;
        }
        static private int find_color(List<color_table_element_t> statistic, byte color)
        {
            for (int i = 0; i < statistic.Count; i++)
                if (statistic[i].color == color)
                    return i;
            return -1;
        }

        //метод сокрытия, использующий одинаковый элементы палитры
        static private bool InCryptInDataWithIdenticalElements(byte[] data, List<byte> message, rgb[] color_table, ref byte origin, ref byte fake)
        {
            //data - данные изображения, содержит индексы цветовой палитры
            //message - скрываемое сообщение
            //origin - оригинальный байт
            //fake - двойник

            if (message.Count < 1)
                return false;

            if (origin == fake)
            {
                //статистика использования элементов палитры
                List<color_table_element_t> color_statistic = new List<color_table_element_t>();
                int index = 0;
                //int old_index = 0;
                foreach (byte i in data)
                {
                    //выполняем поиск очередного индекса из данных изображения
                    //if (!(Convert.ToByte(index) == i)) 
                    index = find_color(color_statistic, i);
                    if (index != -1)
                    {
                        //если индекс найден, увеличиваем его количество
                        color_table_element_t temp;
                        temp.color = color_statistic[index].color;
                        temp.count = color_statistic[index].count + 1;
                        color_statistic[index] = temp;
                    }
                    else
                    {
                        //если индекс не найден, заносим его в статистику
                        color_table_element_t temp;
                        temp.color = i;
                        temp.count = 1;
                        color_statistic.Add(temp);
                    }
                }
                byte max_byte = 0;
                uint max_volue = 0;
                //выполняем поиск цвета с наибольшей частотой появления в файле
                if (!(color_statistic[index].count > message.Count * 8))
                {
                    foreach (color_table_element_t i in color_statistic)
                    {
                        if (i.count > max_volue)
                        {
                            max_volue = i.count;
                            max_byte = i.color;
                        }
                    }
                }
                else
                {
                    max_byte = color_statistic[index].color;
                    max_volue = color_statistic[index].count;
                }

                //оригинальный индекс найден, он равен max_byte
                int fake_byte;
                //выполняем поиск двойника
                for (fake_byte = 0; fake_byte < color_table.Length; fake_byte++)
                {
                    if (color_table[fake_byte].R == color_table[max_byte].R
                        && color_table[fake_byte].G == color_table[max_byte].G
                        && color_table[fake_byte].B == color_table[max_byte].B
                        && fake_byte != max_byte)
                        break;
                }
                //если двойник не найден, мы его создаём, заменяя неиспользуемый элемент палитры
                if (!(fake_byte < color_table.Length))
                {
                    for (fake_byte = 0; fake_byte < color_table.Length; fake_byte++)
                        if (find_color(color_statistic, (byte)fake_byte) == -1)
                            break;
                    color_table[fake_byte].R = color_table[max_byte].R;
                    color_table[fake_byte].G = color_table[max_byte].G;
                    color_table[fake_byte].B = color_table[max_byte].B;
                }
                origin = max_byte;
                fake = (byte)fake_byte;
            }
            int index_of_message = 0;
            int index_of_data = 0;
            BitArray messageArray = new BitArray(8);

            //первые два байта сообщения - размер скрываемой информации
            message.Insert(0, Convert.ToByte(message.Count >> 8));
            message.Insert(0, Convert.ToByte(message.Count - 1 & 0b0000000011111111));
            
            //пока не будет встроенно все сообщение
            while (index_of_message < message.Count && index_of_data < data.Length)
            {
                messageArray = ByteToBit(message[index_of_message]);
                foreach (bool i in messageArray)
                {
                    while (index_of_data < data.Length && data[index_of_data] != origin && data[index_of_data] != fake)
                        index_of_data++;
                    if (i) {
                        //если очередной бит сообщения равен 1, заменяем индекс на "двойника"
                        if (index_of_data < data.Length)
                            data[index_of_data] = fake;
                    }
                    else {
                        //если очередной бит сообщения равен 0, заменяем индекс на "оригинал"
                        if (index_of_data < data.Length)
                            data[index_of_data] = origin;
                    }
                    index_of_data++;
                }
                index_of_message++;
            }
            message.RemoveRange(0, index_of_message);
            return true;
        }

        //метод извлечения сообщения из элементов палитры
        static private bool OutCryptByteLSB(rgb[] list, List<byte> message)
        {
            //list - список всех элементов палитры
            //message - извлекаемое сообщение

            //массив бит для элемента палитры
            BitArray colorArray;
            //массив бит для очередного байта сообщения
            BitArray messageArray = new BitArray(8);

            //элемент палитры
            int index = 0;
            //длина сообщения
            byte length_of_message = 255;
            while (message.Count <= length_of_message)
            {
                //считываем НЗБ из очередного элемента палитры
                colorArray = ByteToBit(list[index].R);
                messageArray[0] = colorArray[0];
                colorArray = ByteToBit(list[index].G);
                messageArray[1] = colorArray[0];
                colorArray = ByteToBit(list[index].B);
                messageArray[2] = colorArray[0];
                messageArray[3] = colorArray[1];
                index++;

                //считываем НЗБ из очередного элемента палитры
                colorArray = ByteToBit(list[index].R);
                messageArray[4] = colorArray[0];
                colorArray = ByteToBit(list[index].G);
                messageArray[5] = colorArray[0];
                colorArray = ByteToBit(list[index].B);
                messageArray[6] = colorArray[0];
                messageArray[7] = colorArray[1];
                message.Add(BitToByte(messageArray));
                index++;

                //первый считанный байт - размер всего сообщения
                if (message.Count == 1)
                {
                    length_of_message = message[0];
                }

            }
            return true;
        }

        //метод извлечения сообщения из данных изображения
        static private bool OutCryptFromDataWithIdenticalElements(byte[] data, List<byte> message, ref int length_of_message, byte original_byte, byte fake_byte)
        {
            //data - данные изображения, содержит индексы цветовой палитры
            //message - извлекаемое сообщение
            //length_of_message - длина сообщения
            //origin - оригинальный байт
            //fake - двойник

            //байт сообщения
            BitArray messageByte = new BitArray(8);

            //очередной индекс изображения
            int index = 0;

            int count = 0;
            //int length_of_message = 65535;
            while (message.Count < length_of_message + 2 && index < data.Length)
            {
                //если индекс ссылается на оригинальный байт - добавляем в сообщение бит 0
                if (data[index] == original_byte)
                {
                    messageByte[count] = false;
                    count++;

                }
                //если индекс ссылается на оригинальный байт - добавляем в сообщение бит 1
                if (data[index] == fake_byte)
                {
                    messageByte[count] = true;
                    count++;
                }
                if (count == 8)
                {
                    message.Add(BitToByte(messageByte));
                    count = 0;

                }
                //первые два считанных элемента - длина сообщения
                if (message.Count == 2)
                {
                    length_of_message = Convert.ToChar(message[0] | message[1] << 8);
                }
                index++;
            }
            return true;
        }

        struct weighed_element_t
        {
            //номер в неотсортированной таблице
            public byte not_sorted_index;
            //вес W
            public int Weight;
        }

        static private byte find_sorted_index(weighed_element_t[] weighted, byte color)
        {
            for (byte i = 0; i < weighted.Length; i++)
                if (weighted[i].not_sorted_index == color)
                    return i;
            return 0;
        }

        static private bool InCryptInDataWithWeighedElements(byte[] data, List<byte> message, rgb[] color_table)
        {
            //создадим таблицу отсортированных цветов по весу W = 65536*R + 256*G + B
            weighed_element_t[] weighed_elements = new weighed_element_t[color_table.Length];
            for (int i = 0; i < color_table.Length; i++)
            {
                weighed_elements[i].not_sorted_index = (byte)i;
                weighed_elements[i].Weight = ((int)(color_table[i].R * 65536 + color_table[i].G * 256 + color_table[i].B));
            }

            //процесс сортировки
            Array.Sort<weighed_element_t>(weighed_elements, (X, y) => (X.Weight.CompareTo(y.Weight)));

            //находим пары индексов, пригодных для сокрытия информации
            List<byte> couples = new List<byte>();
            for (int i = 0; i < weighed_elements.Length - 1; i += 2)
            {
                if (Math.Abs(weighed_elements[i].Weight - weighed_elements[i + 1].Weight) < 1800 &&
                    Math.Abs(color_table[weighed_elements[i].not_sorted_index].B - color_table[weighed_elements[i + 1].not_sorted_index].B) < 25)
                {
                    couples.Add((byte)i);
                    couples.Add(Convert.ToByte(i + 1));
                }
            }

            long max_differ = 15000;

            if(couples.Count == 0)
            {
                for (int i = 0; i < weighed_elements.Length - 1; i += 2)
                {
                    if (Math.Abs(weighed_elements[i].Weight - weighed_elements[i + 1].Weight) < max_differ &&
                        Math.Abs(color_table[weighed_elements[i].not_sorted_index].B - color_table[weighed_elements[i + 1].not_sorted_index].B) < 25)
                    {
                        couples.Add((byte)i);
                        couples.Add(Convert.ToByte(i + 1));
                        max_differ = Math.Abs(weighed_elements[i].Weight - weighed_elements[i + 1].Weight);
                    }
                }
            }

            int index_of_message = 0;
            int index_of_data = 0;

            BitArray messageArray = new BitArray(8);
            //первые два байта сообщения - размер скрываемой информации
            message.Insert(0, Convert.ToByte(message.Count >> 8));
            message.Insert(0, Convert.ToByte(message.Count - 1 & 0b0000000011111111));

            byte sorted_index = 0;
            while (index_of_message < message.Count && index_of_data < data.Length-1)
            {
                if (index_of_data >= data.Length-1)
                    break;
                messageArray = ByteToBit(message[index_of_message]);
                foreach (bool i in messageArray)
                {
                    
                    //sorted_index - адрес в отсортированной таблице цветов
                    if (index_of_data < data.Length)
                        sorted_index = find_sorted_index(weighed_elements, data[index_of_data]);
                    while (!couples.Contains(sorted_index) && index_of_data < data.Length-1)
                    {
                        //пока не найдем индекс, подходящий для скрытия, выполняем цикл while
                        index_of_data++;
                        sorted_index = find_sorted_index(weighed_elements, data[index_of_data]);
                    }
                    if (index_of_data >= data.Length-1)
                        break;
                    if (i)
                    {
                        //если неообходимо скрыть бит сообщения, равный 1
                        //заменяем НЗБ отсортированного индекса, находим 
                        //индекс в оригинальной неотсортированной таблице
                        if (sorted_index % 2 == 0)
                        {
                            sorted_index++;
                            data[index_of_data] = weighed_elements[sorted_index].not_sorted_index;
                        }
                    }
                    else
                    {
                        //если неообходимо скрыть бит сообщения, равный 0
                        //заменяем НЗБ отсортированного индекса, находим 
                        //индекс в оригинальной неотсортированной таблице
                        if (sorted_index % 2 == 1)
                        {
                            sorted_index--;
                            data[index_of_data] = weighed_elements[sorted_index].not_sorted_index;
                        }
                    }
                    index_of_data++;
                }
                index_of_message++;
            }
            message.RemoveRange(0, index_of_message);
            //StegoAnalysis(data, color_table);
            return true;
        }

        static private bool OutCryptFromDataWithWeighedElements(byte[] data, List<byte> message, ref int length_of_message, rgb[] color_table)
        {
            //создадим таблицу отсортированных цветов по весу W = 65536*R + 256*G + B
            weighed_element_t[] weighed_elements = new weighed_element_t[color_table.Length];
            for (int i = 0; i < color_table.Length; i++)
            {
                weighed_elements[i].not_sorted_index = (byte)i;
                weighed_elements[i].Weight = ((int)(color_table[i].R * 65536 + color_table[i].G * 256 + color_table[i].B));
            }
            //процесс сортировки
            Array.Sort<weighed_element_t>(weighed_elements, (X, y) => (X.Weight.CompareTo(y.Weight)));

            //находим пары индексов, пригодных для сокрытия информации
            List<byte> couples = new List<byte>();
            for (int i = 0; i < weighed_elements.Length - 1; i += 2)
            {
                if (Math.Abs(weighed_elements[i].Weight - weighed_elements[i + 1].Weight) < 1800 &&
                    Math.Abs(color_table[weighed_elements[i].not_sorted_index].B - color_table[weighed_elements[i + 1].not_sorted_index].B) < 25)
                {
                    couples.Add((byte)i);
                    couples.Add(Convert.ToByte(i + 1));
                }
            }

            long max_differ = 10000;

            if (couples.Count == 0)
            {
                for (int i = 0; i < weighed_elements.Length - 1; i += 2)
                {
                    if (Math.Abs(weighed_elements[i].Weight - weighed_elements[i + 1].Weight) < max_differ &&
                        Math.Abs(color_table[weighed_elements[i].not_sorted_index].B - color_table[weighed_elements[i + 1].not_sorted_index].B) < 25)
                    {
                        couples.Add((byte)i);
                        couples.Add(Convert.ToByte(i + 1));
                        max_differ = Math.Abs(weighed_elements[i].Weight - weighed_elements[i + 1].Weight);
                    }
                }
            }

            //очередной индекс данных изображения
            int index_of_data = 0;
            BitArray messageByte = new BitArray(8);
            byte sorted_index;
            int count = 0;

            while (message.Count < length_of_message + 2 && index_of_data < data.Length)
            {
                //sorted_index - адрес в отсортированной таблице цветов
                sorted_index = find_sorted_index(weighed_elements, data[index_of_data]);
                //если очередной индекс подходит для скрытия информации
                if (couples.Contains(sorted_index))
                {
                    //извлекаем НЗБ индекса из отсортированной таблицы
                    messageByte[count] = Convert.ToBoolean(sorted_index % 2);
                    count++;

                }
                if (count == 8)
                {
                    message.Add(BitToByte(messageByte));
                    count = 0;

                }
                if (message.Count == 2)
                {
                    //первые два считанных элемента - длина сообщения
                    length_of_message = Convert.ToChar(message[0] | message[1] << 8);
                }
                index_of_data++;
            }
            return true;
        }

        static double factorial(double n)
        {
            if (n == 0) { return 1; }
            if (n < 0) { return Math.Sqrt(Math.PI); }
            return n * factorial(n - 1);
        }

        // gamma function
        static double gamma(double n)
        {
            return factorial(n - 1);
        }

        // chi square distribution function (v.1)
        static double chi_square_distribution(double x, double v)
        {
            double a = v / 2, b = x / 2;

            // count the Gamma function 
            double g = gamma(a);

            // count of the probability density 
            double f = Math.Pow(x, a - 1) / g / Math.Exp(b) / Math.Pow(2, a);

            // count the sum of series
            double s = 0, z = 0;
            int k = 0, w = 0;
            g = 1;
            do
            {
                k = k + 1;
                w = w + 2;
                z = s;
                g = g * (v + w);
                s = s + (Math.Pow(x, k) / g);
            } while (s != z);

            // count of probabilities
            double p = 2 * x * f * (1 + s) / v;
            double q = 1 - p;

            return q;
        }

        // chi square distribution function (v.2)
        static double _chi_square_distribution(double x, double v)
        {
            double a = v / 2, b = x / 2, g = 1;

            // count the Gamma function... 
            // for even degree
            if ((int)(v) % 2 == 0)
            {
                g = 1;
                for (int i = 1; i < a; i++)
                {
                    g = g * i;
                }
            }
            // for odd degree
            if ((int)(v) % 2 != 0)
            {
                g = Math.Sqrt(Math.PI);
                for (double i = 0.5; i < a; i++)
                {
                    g = g * i;
                }
            }

            // count of the probability density 
            double f = Math.Pow(x, a - 1) / g / Math.Exp(b) / Math.Pow(2, a);

            // count the sum of series
            double s = 0, z = 0;
            int k = 0, w = 0;
            g = 1;
            do
            {
                k = k + 1;
                w = w + 2;
                z = s;
                g = g * (v + w);
                s = s + (Math.Pow(x, k) / g);
            } while (s != z && Math.Abs(s - z) > 0.001);

            // count of probabilities
            double p = 2 * x * f * (1 + s) / v;
            double q = 1 - p;

            return q;
        }

        // recursio search chi square inverse
        static double search_chi_square(double x, double h, double p, double v)
        {
            double z = x;
            x = x + h;
            double y = _chi_square_distribution(x, v);

            if (z == x || y == p)
            {
                return x;
            }
            if (y < p)
            {
                return search_chi_square(x - h, h / 2, p, v);
            }
            if (y > p)
            {
                return search_chi_square(x, h, p, v);
            }
            return 0; // warning control reaches end of non-void function -wreturn-type
        }

        // chi shuare inverse
        static double chi_square_inverse(double p, double v)
        {
            return search_chi_square(0, 0.1, p, v);
        }

        static bool StegoAnalysis(byte[] data, rgb[] color_table, List<double> propability)
        {
            //создадим таблицу отсортированных цветов по весу W = 65536*R + 256*G + B
            weighed_element_t[] weighed_elements = new weighed_element_t[color_table.Length];
            for (int i = 0; i < color_table.Length; i++)
            {
                weighed_elements[i].not_sorted_index = (byte)i;
                weighed_elements[i].Weight = ((int)(color_table[i].R * 65536 + color_table[i].G * 256 + color_table[i].B));
            }
            //процесс сортировки
            Array.Sort<weighed_element_t>(weighed_elements, (X, y) => (X.Weight.CompareTo(y.Weight)));

            int index_of_data = 0;
            //размер последовательности индексов для анализа
            int size_of_blocks = 1;
            while (size_of_blocks < data.Length / size_of_blocks)
            {
                size_of_blocks = size_of_blocks << 1;
            }
            
            //шаг между последовательностями изображения
            int step = data.Length / size_of_blocks;

            for (index_of_data = 0; index_of_data < step; index_of_data++)
            {
                int[] EmperHistogramm = new int[color_table.Length];

                byte sorted_index = 0;
                int index_of_block = 0;
                //построение эмперической гистограммы
                while (index_of_block < size_of_blocks && (index_of_data * size_of_blocks + index_of_block) < data.Length)
                {
                    sorted_index = find_sorted_index(weighed_elements, data[index_of_data * size_of_blocks + index_of_block]);
                    EmperHistogramm[sorted_index]++;
                    index_of_block++;
                }
                int[] TeorHistogramm = new int[color_table.Length];

                //построение теоретической гистограммы
                for (int i = 0; i < TeorHistogramm.Length; i += 2)
                {
                    TeorHistogramm[i] = (EmperHistogramm[i] + EmperHistogramm[i + 1]) / 2;
                    TeorHistogramm[i + 1] = (EmperHistogramm[i] + EmperHistogramm[i + 1]) / 2;
                }

                double Xi2 = 0;

                //подсчет характеристики Хи-квадрат
                for (int i = 0; i < EmperHistogramm.Length; i++)
                {
                    if (TeorHistogramm[i] != 0)
                    {
                        Xi2 += Math.Pow((EmperHistogramm[i] - TeorHistogramm[i]), 2) / TeorHistogramm[i];
                    }
                }
                //вычисление вероятности сокрытия информации в данном блоке изображения
                propability.Add(_chi_square_distribution(Xi2, color_table.Length-1)*100.0);
            }
            return true;
        }


        static bool write_sub_blocks(BinaryReader gif_file, List<byte> data)
        {
            byte[] data_array = data.ToArray();
            int i = 0;
            byte block_size;
            while (i < data_array.Length)
            {
                block_size = Convert.ToByte((data_array.Length - i) >= 255 ? 255 : (data_array.Length - i));
                NewFile.WriteByte(block_size);
                NewFile.Write(data_array, i, block_size);
                i += (block_size);
            }
            return true;
        }

        static int read_sub_blocks(BinaryReader gif_file, List<byte> data)
        {
            int data_length;
            int index;
            byte block_size;
            data_length = 0;
            index = 0;

            while (true)
            {
                block_size = gif_file.ReadByte();
                if (block_size == 0)  // end of sub-blocks
                {
                    break;
                }
                data_length += block_size;
                for (int i = 0; i < block_size; i++)
                {
                    data.Add(OriginBR.ReadByte());
                }
                index += block_size;
            }
            return data_length;
        }

        public static bool uncompress(int code_length,
                    List<byte> input,
                    int input_length,
                    byte[] outf)
        {
            int i, j = 0, bit;
            int code, prev = -1;
            dictionary_entry_t[] dictionary;
            int dictionary_ind;
            uint mask = 0x01; //2^12
            int reset_code_length;
            int clear_code; // This varies depending on code_length
            int stop_code;  // one more than clear code
            int match_len = 0;
            int length = 0;

            clear_code = 1 << (code_length);
            stop_code = clear_code + 1;
            reset_code_length = code_length;

            dictionary = new dictionary_entry_t[4096];

            for (dictionary_ind = 0;
                dictionary_ind < (1 << code_length);
                dictionary_ind++)
            {
                dictionary[dictionary_ind].b = Convert.ToByte(dictionary_ind);
                dictionary[dictionary_ind].prev = -1;
                dictionary[dictionary_ind].len = 1;
            }

            dictionary_ind++;
            dictionary_ind++;
            code = 0x0;
            while (input_length > 0)
            {
                code = 0x0;
                for (i = 0; i < (code_length + 1); i++)
                {
                    bit = Convert.ToBoolean(input[j] & mask) ? 1 : 0;
                    mask <<= 1;
                    if (mask == 0x100)
                    {
                        mask = 0x01;
                        j++;
                        input_length--;
                    }
                    code = code | (bit << i);
                }

                if (code == clear_code)
                {
                    code_length = reset_code_length;
                    for (dictionary_ind = 0;
                    dictionary_ind < (1 << code_length);
                    dictionary_ind++)
                    {
                        dictionary[dictionary_ind].b = Convert.ToByte(dictionary_ind);
                        dictionary[dictionary_ind].prev = -1;
                        dictionary[dictionary_ind].len = 1;
                    }
                    dictionary_ind++;
                    dictionary_ind++;
                    prev = -1;
                    continue;
                }
                else if (code == stop_code)
                {
                    if (input_length > 1)
                    {
                        return false;
                    }
                    break;
                }

                if ((prev > -1) && (code_length < 12))
                {
                    if (code > dictionary_ind)
                    {
                        return false;
                    }
                    if (code == dictionary_ind)
                    {
                        int ptr = prev;

                        while (dictionary[ptr].prev != -1)
                        {
                            ptr = dictionary[ptr].prev;
                        }
                        dictionary[dictionary_ind].b = dictionary[ptr].b;
                    }
                    else
                    {
                        int ptr = code;
                        while (dictionary[ptr].prev != -1)
                        {
                            ptr = dictionary[ptr].prev;
                        }
                        dictionary[dictionary_ind].b = dictionary[ptr].b;
                    }
                    dictionary[dictionary_ind].prev = prev;
                    dictionary[dictionary_ind].len = dictionary[prev].len + 1;
                    dictionary_ind++;
                    if ((dictionary_ind == (1 << (code_length + 1))) &&
                         (code_length < 11))
                    {
                        code_length++;
                    }
                }

                prev = code;
                length = dictionary[code].len;
                while (code != -1)
                {
                    outf[dictionary[code].len - 1 + match_len] = dictionary[code].b;
                    if (dictionary[code].prev == code)
                    {
                        return false;
                    }
                    code = dictionary[code].prev;
                }
                match_len += length;
            }
            return true;
        }

        public struct compress_dictionary_t
        {
            public byte[] str;
        }
        public static int find_dictionary_elem(compress_dictionary_t[] dictionary, byte[] b, int len, int index)
        {
            for (int i = index; i < len; i++)
            {
                if (dictionary[i].str != null)
                    if (dictionary[i].str.Length == b.Length)
                    {
                        if (dictionary[i].str.SequenceEqual(b))
                            return i;
                    }
            }
            return -1;
        }

        public static bool compress(int code_length,
                    byte[] input,
                    int input_length,
                    List<byte> output)
        {
            int i = 0;
            int code, prev = 0;
            int step = 0;
            int dictionary_ind;
            int reset_code_length;
            bool bit;
            BitArray bits = new BitArray(8);
            int clear_code; // This varies depending on code_length
            int stop_code;  // one more than clear code
            int count = 0;
            clear_code = 1 << (code_length);
            code = clear_code;

            for (int k = 0; k < (code_length + 1); k++)
            {
                bit = Convert.ToBoolean(code % 2);
                bits[count] = bit;
                code = code / 2;
                count++;
                if (count == 8)
                {
                    count = 0;
                    output.Add(BitToByte(bits));
                }
            }

            stop_code = clear_code + 1;
            reset_code_length = code_length;
            compress_dictionary_t[] dictionary = new compress_dictionary_t[4096];
            for (dictionary_ind = 0; dictionary_ind < (1 << reset_code_length); dictionary_ind++)
            {
                dictionary[dictionary_ind].str = new byte[1];
                dictionary[dictionary_ind].str[0] = Convert.ToByte(dictionary_ind);
            }
            dictionary_ind++;
            dictionary_ind++;
            byte[] temp;
            while (i < input.Length)
            {
                temp = new byte[step + 1];
                for (int j = 0; j < temp.Length; j++)
                    temp[j] = input[i + j];
                prev = find_dictionary_elem(dictionary, temp, dictionary_ind, prev);
                if (prev != -1)
                {
                    if (i + step + 1 < input.Length)
                    {
                        step++;
                        code = prev;
                    }
                    else
                    {
                        code = prev;
                        for (int k = 0; k < (code_length + 1); k++)
                        {
                            bit = Convert.ToBoolean(code % 2);
                            bits[count] = bit;
                            code = code / 2;
                            count++;
                            if (count == 8)
                            {
                                count = 0;
                                output.Add(BitToByte(bits));
                            }
                        }
                        break;
                    }
                }
                else
                {
                    for (int k = 0; k < (code_length + 1); k++)
                    {
                        bit = Convert.ToBoolean(code % 2);
                        bits[count] = bit;
                        code = code / 2;
                        count++;
                        if (count == 8)
                        {
                            count = 0;
                            output.Add(BitToByte(bits));
                        }
                    }
                    i += (step);
                    if (dictionary_ind < 4096)
                    {
                        if (dictionary_ind == ((1 << (code_length + 1))) &&
                         (code_length < 11))
                            code_length++;
                        dictionary[dictionary_ind].str = temp;
                        dictionary_ind++;
                    }
                    else
                    {
                        code = clear_code;
                        for (int k = 0; k < (code_length + 1); k++)
                        {
                            bit = Convert.ToBoolean(code % 2);
                            bits[count] = bit;
                            code = code / 2;
                            count++;
                            if (count == 8)
                            {
                                count = 0;
                                output.Add(BitToByte(bits));
                            }
                        }
                        code_length = reset_code_length;
                        dictionary = new compress_dictionary_t[4096];

                        for (dictionary_ind = 0; dictionary_ind < (1 << reset_code_length); dictionary_ind++)
                        {
                            dictionary[dictionary_ind].str = new byte[1];
                            dictionary[dictionary_ind].str[0] = Convert.ToByte(dictionary_ind);
                        }
                        dictionary_ind++;
                        dictionary_ind++;
                    }
                    step = 0;
                    prev = 0;
                }
            }

            code = stop_code;
            for (int k = 0; k < (code_length + 1); k++)
            {
                bit = Convert.ToBoolean(code % 2);
                bits[count] = bit;
                code = code / 2;
                count++;
                if (count == 8)
                {
                    count = 0;
                    output.Add(BitToByte(bits));
                }
            }

            while (count < 8)
            {
                bits[count] = false;
                count++;
            }
            output.Add(BitToByte(bits));
            return true;
        }

        static bool process_image_descriptor(BinaryReader gif_file,
                                 rgb[] gct,
                                 List<byte> message,
                                 int resolution_bits, 
                                 int steg_methode)
        {
            image_descriptor_t image_descriptor;
            bool disposition;
            int compressed_data_length;
            List<byte> compressed_data = new List<byte>();
            byte lzw_code_size;
            int uncompressed_data_length = 0;
            byte[] uncompressed_data;

            image_descriptor.left_position = Convert.ToChar(OriginBR.ReadByte() | OriginBR.ReadByte() << 8);
            image_descriptor.top_position = Convert.ToChar(OriginBR.ReadByte() | OriginBR.ReadByte() << 8);
            image_descriptor.width = Convert.ToChar(OriginBR.ReadByte() | OriginBR.ReadByte() << 8);
            image_descriptor.height = Convert.ToChar(OriginBR.ReadByte() | OriginBR.ReadByte() << 8);
            image_descriptor.fields = OriginBR.ReadByte();

            ushort local_color_table_size = Convert.ToChar(1 << ((image_descriptor.fields & 0x07) + 1));

            NewFile.WriteByte(Convert.ToByte(image_descriptor.left_position & 0b0000000011111111));
            NewFile.WriteByte(Convert.ToByte(image_descriptor.left_position >> 8));
            NewFile.WriteByte(Convert.ToByte(image_descriptor.top_position & 0b0000000011111111));
            NewFile.WriteByte(Convert.ToByte(image_descriptor.top_position >> 8));
            NewFile.WriteByte(Convert.ToByte(image_descriptor.width & 0b0000000011111111));
            NewFile.WriteByte(Convert.ToByte(image_descriptor.width >> 8));
            NewFile.WriteByte(Convert.ToByte(image_descriptor.height & 0b0000000011111111));
            NewFile.WriteByte(Convert.ToByte(image_descriptor.height >> 8));
            NewFile.WriteByte(image_descriptor.fields);

            rgb[] local_color_table = new rgb[local_color_table_size];
            if (Convert.ToBoolean(image_descriptor.fields) && local_color_table_size > 0)
            {
                for (int i = 0; i < local_color_table_size; i++) //local_color_table_size
                {
                    local_color_table[i].R = OriginBR.ReadByte();
                    //NewFile.WriteByte(local_color_table[i].R);
                    local_color_table[i].G = OriginBR.ReadByte();
                    //NewFile.WriteByte(local_color_table[i].G);
                    local_color_table[i].B = OriginBR.ReadByte();
                    //NewFile.WriteByte(local_color_table[i].B);
                }

                if (steg_methode == 1)
                {
                    if ((message.Count + 1) * 2 < local_color_table_size)
                    {
                        int index = 0;
                        message.Insert(0, Convert.ToByte(message.Count));
                        foreach (byte i in message)
                        {
                            InCryptByteLSB(local_color_table, i, index);
                            index += 2;
                        }

                    }
                    else
                    {
                        MessageBox.Show("Слишком большое сообщение!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                }

                foreach (rgb i in local_color_table)
                {
                    NewFile.WriteByte(i.R);
                    NewFile.WriteByte(i.G);
                    NewFile.WriteByte(i.B);
                }
            }
            else
            {

            }
            disposition = true;
            lzw_code_size = OriginBR.ReadByte();
            NewFile.WriteByte(lzw_code_size);

            compressed_data_length = read_sub_blocks(gif_file, compressed_data);

            if (steg_methode == 2 || steg_methode == 3)
            {

                uncompressed_data_length = image_descriptor.width *
                                            image_descriptor.height;
                uncompressed_data = new byte[uncompressed_data_length];

                uncompress(lzw_code_size, compressed_data, compressed_data_length,
                  uncompressed_data);
                List<byte> byte_array = new List<byte>();

                byte origin = 0;
                byte fake = 0;

                if (steg_methode == 2)
                {
                    if (Convert.ToBoolean(image_descriptor.fields))
                        InCryptInDataWithIdenticalElements(uncompressed_data, message, local_color_table, ref origin, ref fake);
                    else
                        InCryptInDataWithIdenticalElements(uncompressed_data, message, gct, ref origin, ref fake);
                    string mes = "Origin byte = " + origin.ToString() + "; fake byte = " + fake.ToString() + ".";
                    if (show_bytes)
                    {
                        MessageBox.Show(mes, "Запомните данные байты", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        show_bytes = false;
                    }
                }

                if (steg_methode == 3)
                {
                    if (Convert.ToBoolean(image_descriptor.fields))
                        InCryptInDataWithWeighedElements(uncompressed_data, message, local_color_table);
                    else
                        InCryptInDataWithWeighedElements(uncompressed_data, message, gct);
                }

                
                compress(lzw_code_size, uncompressed_data, 0, byte_array);
                write_sub_blocks(gif_file, byte_array);
            }
            else
                write_sub_blocks(gif_file, compressed_data);
            NewFile.WriteByte(0);
            return disposition;

        }

        static bool process_image_descriptor(BinaryReader gif_file,
                                 rgb[] gct,
                                 List<byte> message,
                                 int steg_methode, 
                                 ref int message_size)
        {
            image_descriptor_t image_descriptor;
            bool disposition;
            int compressed_data_length;
            List<byte> compressed_data = new List<byte>();
            byte lzw_code_size;
            int uncompressed_data_length = 0;
            byte[] uncompressed_data;

            image_descriptor.left_position = Convert.ToChar(OriginBR.ReadByte() | OriginBR.ReadByte() << 8);
            image_descriptor.top_position = Convert.ToChar(OriginBR.ReadByte() | OriginBR.ReadByte() << 8);
            image_descriptor.width = Convert.ToChar(OriginBR.ReadByte() | OriginBR.ReadByte() << 8);
            image_descriptor.height = Convert.ToChar(OriginBR.ReadByte() | OriginBR.ReadByte() << 8);
            image_descriptor.fields = OriginBR.ReadByte();

            ushort local_color_table_size = Convert.ToChar(1 << ((image_descriptor.fields & 0x07) + 1));

            // TODO if LCT = true, read the LCT
            rgb[] local_color_table = new rgb[local_color_table_size];
            if (Convert.ToBoolean(image_descriptor.fields) && local_color_table_size > 0)
            {
                for (int i = 0; i < local_color_table_size; i++) //local_color_table_size
                {
                    local_color_table[i].R = OriginBR.ReadByte();
                    //NewFile.WriteByte(local_color_table[i].R);
                    local_color_table[i].G = OriginBR.ReadByte();
                    //NewFile.WriteByte(local_color_table[i].G);
                    local_color_table[i].B = OriginBR.ReadByte();
                    //NewFile.WriteByte(local_color_table[i].B);
                }

                if (steg_methode == 1)
                {

                    OutCryptByteLSB(local_color_table, message);
                    //string outcrypt_mes = Encoding.UTF8.GetString(message.ToArray(), 1, message.Count - 1);
                    return true;
                }


            }
            else
            {

            }
            disposition = true;

            lzw_code_size = OriginBR.ReadByte();
            //NewFile.WriteByte(lzw_code_size);

            compressed_data_length = read_sub_blocks(gif_file, compressed_data);

            if (steg_methode == 2 || steg_methode == 3)
            {

                uncompressed_data_length = image_descriptor.width *
                                            image_descriptor.height;
                uncompressed_data = new byte[uncompressed_data_length];

                uncompress(lzw_code_size, compressed_data, compressed_data_length,
                  uncompressed_data);
                //List<int> uncompdata = new List<int>();

                if(steg_methode == 2)
                    OutCryptFromDataWithIdenticalElements(uncompressed_data, message, ref message_size, OriginByte, FakeByte);

                if (steg_methode == 3)
                {
                    if (Convert.ToBoolean(image_descriptor.fields))
                        OutCryptFromDataWithWeighedElements(uncompressed_data, message, ref message_size, local_color_table);
                    else
                        OutCryptFromDataWithWeighedElements(uncompressed_data, message, ref message_size, gct);
                }

            }

            return disposition;

        }

        static bool process_extension(BinaryReader gif_file)
        {
            extension_t extension;
            graphic_control_extension_t gce;
            application_extension_t application;
            plaintext_extension_t plaintext;
            List<byte> extension_data = new List<byte>();
            int extension_data_length;

            extension.extension_code = OriginBR.ReadByte();
            extension.block_size = OriginBR.ReadByte();

            NewFile.WriteByte(extension.extension_code);
            NewFile.WriteByte(extension.block_size);

            switch (extension.extension_code)
            {
                case Constants.GRAPHIC_CONTROL:

                    gce.fields = OriginBR.ReadByte();
                    gce.delay_time = Convert.ToChar(OriginBR.ReadByte() | OriginBR.ReadByte() << 8);
                    gce.transparent_color_index = OriginBR.ReadByte();

                    NewFile.WriteByte(gce.fields);
                    NewFile.WriteByte(Convert.ToByte(gce.delay_time & 0b0000000011111111));
                    NewFile.WriteByte(Convert.ToByte(gce.delay_time >> 8));
                    NewFile.WriteByte(gce.transparent_color_index);

                    break;
                case Constants.APPLICATION_EXTENSION:

                    application.application_id = Encoding.UTF8.GetString(OriginBR.ReadBytes(8));
                    application.version = Encoding.UTF8.GetString(OriginBR.ReadBytes(3));

                    
                    NewFile.Write(Encoding.ASCII.GetBytes(application.application_id), 0,
                        Encoding.ASCII.GetBytes(application.application_id).Length);
                    NewFile.Write(Encoding.ASCII.GetBytes(application.version), 0,
                        Encoding.ASCII.GetBytes(application.version).Length);
                    break;
                case 0xFE:
                    // comment extension; do nothing - all the data is in the
                    // sub-blocks that follow.
                    byte comments = 0;
                    for(int i = 0; i< extension.block_size; i++)
                    {
                        comments = OriginBR.ReadByte();
                        NewFile.WriteByte(comments);
                    }
                    break;
                case 0x01:

                    plaintext.left = Convert.ToChar(OriginBR.ReadByte() | OriginBR.ReadByte() << 8);
                    plaintext.top = Convert.ToChar(OriginBR.ReadByte() | OriginBR.ReadByte() << 8);
                    plaintext.width = Convert.ToChar(OriginBR.ReadByte() | OriginBR.ReadByte() << 8);
                    plaintext.height = Convert.ToChar(OriginBR.ReadByte() | OriginBR.ReadByte() << 8);
                    plaintext.cell_width = OriginBR.ReadByte();
                    plaintext.cell_height = OriginBR.ReadByte();
                    plaintext.background_color = OriginBR.ReadByte();

                    NewFile.WriteByte(Convert.ToByte(plaintext.left & 0b0000000011111111));
                    NewFile.WriteByte(Convert.ToByte(plaintext.left >> 8));
                    NewFile.WriteByte(Convert.ToByte(plaintext.top & 0b0000000011111111));
                    NewFile.WriteByte(Convert.ToByte(plaintext.top >> 8));
                    NewFile.WriteByte(Convert.ToByte(plaintext.width & 0b0000000011111111));
                    NewFile.WriteByte(Convert.ToByte(plaintext.width >> 8));
                    NewFile.WriteByte(Convert.ToByte(plaintext.height & 0b0000000011111111));
                    NewFile.WriteByte(Convert.ToByte(plaintext.height >> 8));

                    NewFile.WriteByte(plaintext.cell_width);
                    NewFile.WriteByte(plaintext.cell_height);
                    NewFile.WriteByte(plaintext.background_color);

                    break;
                default:
                    //fprintf(stderr, "Unrecognized extension code.\n");
                    //exit(0);
                    break;
            }

            // All extensions are followed by data sub-blocks; even if it's
            // just a single data sub-block of length 0
            extension_data_length = read_sub_blocks(gif_file, extension_data);
            write_sub_blocks(gif_file, extension_data);
            NewFile.WriteByte(0);


            return true;
        }

        public static void process_gif_stream(BinaryReader gif_file, List<byte> message, int steg_methode)
        {

            count_of_pictures = 0;
            byte[] header = OriginBR.ReadBytes(6);

            screen_descriptor_t screen_descriptor;
            int color_resolution_bits;

            int global_color_table_size = 0;  // number of entries in global_color_table
            rgb[] global_color_table = null;

            byte block_type = 0x0;

            // A GIF file starts with a Header (section 17)

            // XXX there's another format, GIF87a, that you may still find
            // floating around.
            if (Convert.ToBoolean(String.Compare("GIF89a", Encoding.UTF8.GetString(header))))
            {
                MessageBox.Show("Неверный формат изображения!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            NewFile.Write(header, 0, header.Length);

            // Followed by a logical screen descriptor
            // Note that this works because GIFs specify little-endian order; on a
            // big-endian machine, the height & width would need to be reversed.

            // Can't use sizeof here since GCC does byte alignment; 
            // sizeof( screen_descriptor_t ) = 8!
            screen_descriptor.width = Convert.ToChar(OriginBR.ReadByte() | OriginBR.ReadByte() << 8);

            screen_descriptor.height = Convert.ToChar(OriginBR.ReadByte() | OriginBR.ReadByte() << 8);

            screen_descriptor.fields = OriginBR.ReadByte();

            screen_descriptor.background_color = OriginBR.ReadByte();
            screen_descriptor.ratio = OriginBR.ReadByte();

            //NewFile.Write(Encoding.ASCII.GetBytes());
            NewFile.WriteByte(Convert.ToByte(screen_descriptor.width & 0b0000000011111111));
            NewFile.WriteByte(Convert.ToByte(screen_descriptor.width >> 8));

            NewFile.WriteByte(Convert.ToByte(screen_descriptor.height & 0b0000000011111111));
            NewFile.WriteByte(Convert.ToByte(screen_descriptor.height >> 8));
            NewFile.WriteByte(screen_descriptor.fields);
            NewFile.WriteByte(screen_descriptor.background_color);
            NewFile.WriteByte(screen_descriptor.ratio);


            color_resolution_bits = ((screen_descriptor.fields & 0x70) >> 4) + 1;

            if (Convert.ToBoolean(screen_descriptor.fields >> 7))
            {
                //int i;
                // If bit 7 is set, the next block is a global color table; read it

                global_color_table_size = 1 <<
                  (((screen_descriptor.fields & 0x07) + 1));

                global_color_table = new rgb[global_color_table_size];

                // XXX this could conceivably return a short count...

                for (int i = 0; i < global_color_table_size; i++) //local_color_table_size
                {
                    global_color_table[i].R = OriginBR.ReadByte();

                    global_color_table[i].G = OriginBR.ReadByte();

                    global_color_table[i].B = OriginBR.ReadByte();

                }

                if (steg_methode == 1)
                {
                    if ((message.Count + 1) * 2 < global_color_table_size)
                    {
                        int index = 0;
                        message.Insert(0, Convert.ToByte(message.Count));
                        foreach (byte i in message)
                        {
                            InCryptByteLSB(global_color_table, i, index);
                            index += 2;
                        }
                        
                    }
                    else
                    {
                        MessageBox.Show("Слишком большое сообщение!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }

                foreach (rgb i in global_color_table)
                {
                    NewFile.WriteByte(i.R);
                    NewFile.WriteByte(i.G);
                    NewFile.WriteByte(i.B);
                }

            }

            while (block_type != Constants.TRAILER)
            {
                block_type = OriginBR.ReadByte();
                NewFile.WriteByte(block_type);
                switch (block_type)
                {
                    case Constants.IMAGE_DESCRIPTOR:
                        if (!process_image_descriptor(gif_file,
                                global_color_table,
                                message,
                                color_resolution_bits, steg_methode))
                        {
                            return;
                        }
                        if (steg_methode == 1)
                            steg_methode = 0;
                        count_of_pictures++;
                        break;
                    case Constants.EXTENSION_INTRODUCER:
                        if (!process_extension(gif_file))
                        {
                            return;
                        }
                        break;
                    case Constants.TRAILER:
                        break;
                    default:

                        return;
                }
            }
        }

        static bool process_extension(BinaryReader gif_file, bool outcryption)
        {
            extension_t extension;
            graphic_control_extension_t gce;
            application_extension_t application;
            plaintext_extension_t plaintext;
            List<byte> extension_data = new List<byte>();
            int extension_data_length;

            extension.extension_code = OriginBR.ReadByte();
            extension.block_size = OriginBR.ReadByte();

            switch (extension.extension_code)
            {
                case Constants.GRAPHIC_CONTROL:

                    gce.fields = OriginBR.ReadByte();
                    gce.delay_time = Convert.ToChar(OriginBR.ReadByte() | OriginBR.ReadByte() << 8);
                    gce.transparent_color_index = OriginBR.ReadByte();

                    break;
                case Constants.APPLICATION_EXTENSION:

                    application.application_id = Encoding.UTF8.GetString(OriginBR.ReadBytes(8));
                    application.version = Encoding.UTF8.GetString(OriginBR.ReadBytes(3));

                    break;
                case 0xFE:
                    // comment extension; do nothing - all the data is in the
                    // sub-blocks that follow.
                    byte comments = 0;
                    for (int i = 0; i < extension.block_size; i++)
                    {
                        comments = OriginBR.ReadByte();
                        //NewFile.WriteByte(comments);
                    }
                    break;
                case 0x01:

                    plaintext.left = Convert.ToChar(OriginBR.ReadByte() | OriginBR.ReadByte() << 8);
                    plaintext.top = Convert.ToChar(OriginBR.ReadByte() | OriginBR.ReadByte() << 8);
                    plaintext.width = Convert.ToChar(OriginBR.ReadByte() | OriginBR.ReadByte() << 8);
                    plaintext.height = Convert.ToChar(OriginBR.ReadByte() | OriginBR.ReadByte() << 8);
                    plaintext.cell_width = OriginBR.ReadByte();
                    plaintext.cell_height = OriginBR.ReadByte();
                    plaintext.background_color = OriginBR.ReadByte();

                    break;
                default:
                    break;
            }

            extension_data_length = read_sub_blocks(gif_file, extension_data);

            return true;
        }

        public static string process_gif_stream(BinaryReader gif_file, int steg_methode)
        {
            count_of_pictures = 0;
            List<byte> message = new List<byte>();
            byte[] header = OriginBR.ReadBytes(6);
            screen_descriptor_t screen_descriptor;
            int color_resolution_bits;
            int global_color_table_size = 0;  // number of entries in global_color_table
            rgb[] global_color_table = null;

            byte block_type = 0x0;
            if (Convert.ToBoolean(String.Compare("GIF89a", Encoding.UTF8.GetString(header))))
            {
                MessageBox.Show("Неверный формат изображения!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return "";
            }
            screen_descriptor.width = Convert.ToChar(OriginBR.ReadByte() | OriginBR.ReadByte() << 8);

            screen_descriptor.height = Convert.ToChar(OriginBR.ReadByte() | OriginBR.ReadByte() << 8);

            screen_descriptor.fields = OriginBR.ReadByte();

            screen_descriptor.background_color = OriginBR.ReadByte();

            screen_descriptor.ratio = OriginBR.ReadByte();

            color_resolution_bits = ((screen_descriptor.fields & 0x70) >> 4) + 1;

            if (Convert.ToBoolean(screen_descriptor.fields >> 7))
            {
                global_color_table_size = 1 <<
                  (((screen_descriptor.fields & 0x07) + 1));

                global_color_table = new rgb[global_color_table_size];
                for (int i = 0; i < global_color_table_size; i++) //local_color_table_size
                {
                    global_color_table[i].R = OriginBR.ReadByte();

                    global_color_table[i].G = OriginBR.ReadByte();

                    global_color_table[i].B = OriginBR.ReadByte();

                }
                if (steg_methode == 1)
                {
                    OutCryptByteLSB(global_color_table, message);
                    string outcrypt_mes = Encoding.UTF8.GetString(message.ToArray(), 1, message.Count - 1);
                    return outcrypt_mes;
                }

            }
            int message_size = 65535;
            while (block_type != Constants.TRAILER)
            {
                block_type = OriginBR.ReadByte();
                switch (block_type)
                {
                    case Constants.IMAGE_DESCRIPTOR:
                        process_image_descriptor(gif_file,
                                global_color_table,
                                message,
                                steg_methode, ref message_size);
                        if (steg_methode == 1)
                            return Encoding.UTF8.GetString(message.ToArray(), 1, message.Count-1);
                        if (steg_methode == 2 || steg_methode == 3)
                        {
                            if (message.Count == message_size + 2)
                            {
                                string outcrypt_mes = Encoding.UTF8.GetString(message.ToArray(), 1, message.Count - 1);
                                return outcrypt_mes;
                            }
                        }

                        count_of_pictures++;
                        break;
                    case Constants.EXTENSION_INTRODUCER:
                        if (!process_extension(gif_file, false))
                        {
                            return "";
                        }
                        break;
                    case Constants.TRAILER:
                        break;
                    default:
                        return "";
                }
            }
            if (message.Count > 2)
                return Encoding.UTF8.GetString(message.ToArray(), 1, message.Count - 1);
            else
                return "";
        }

        private void button3_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Алгоритм втраивания прошел успешно. Сохраните изображение.", "Завершение", MessageBoxButtons.OK, MessageBoxIcon.Information);
            string NewFileName;
            SaveFileDialog dSavePic = new SaveFileDialog();
            dSavePic.Filter = "Файлы изображений (*.gif)|*.gif|Все файлы (*.*)|*.*";
            if (dSavePic.ShowDialog() == DialogResult.OK)
            {
                NewFileName = dSavePic.FileName;
            }
            else
            {
                NewFileName = "";
                return;
            };

            
            try
            {
                NewFile = new FileStream(NewFileName, FileMode.Create); //открываем поток на запись результатов
            }
            catch (IOException)
            {
                MessageBox.Show("Ошибка открытия файла на запись", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            NewBR = new BinaryWriter(NewFile, Encoding.UTF8);

            List<byte> messageList = new List<byte>();
            while (MessageBR.PeekChar() != -1)
            { //считали весь текстовый файл для шифрования в лист байт
                messageList.Add(MessageBR.ReadByte());
            }
            int stegano_method;
            if (radioButton1.Checked)
                stegano_method = 1;
            else
            {
                if (radioButton2.Checked)
                    stegano_method = 2;
                else
                    stegano_method = 3;
            }

            if (OriginBR != null && MessageBR != null && NewFile != null)
            {
                show_bytes = true;
                process_gif_stream(OriginBR, messageList, stegano_method);
                
                
            }

            if (OriginBR != null)
            {
                OriginBR.Close();
                OriginBR = null;
            }
            if (OriginFile != null)
            {
                OriginFile.Close();
                OriginFile = null;
            }
            if (MessageBR != null)
            {
                MessageBR.Close();
                MessageBR = null;
            }
            if (MessageFile != null)
            {
                MessageFile.Close();
                MessageFile = null;
            }
            if (NewBR != null)
            {
                NewBR.Close();
                NewBR = null;
            }
            if (NewFile != null)
            {
                NewFile.Close();
                NewFile = null;
            }

            try
            {
                ActiveImage = Image.FromFile(NewFileName);
                pictureBox2.Image = NoImage;
                pictureBox1.Image = ActiveImage;
            }
            catch (IOException)
            {
                return;
            }

            textBox1.Text = "";
            textBox2.Text = "";
        }

        private void radioButton3_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton3.Checked)
            {
                panel5.Visible = true;
                panel5.Enabled = true;
            }
            else
            {
                panel5.Visible = false;
                panel5.Enabled = false;
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {

            int stegano_methode;

            if (radioButton4.Checked)
                stegano_methode = 1;
            else
            {
                if (radioButton3.Checked)
                    stegano_methode = 2;
                else
                    stegano_methode = 3;
            }

            if (stegano_methode == 2)
            {
                if (textBox3.Text != "" && textBox5.Text != "")
                {
                    if(Convert.ToInt16(textBox3.Text) > 255 || Convert.ToInt16(textBox3.Text) <0)
                    {
                        MessageBox.Show("Данные введены некорректно", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    else
                        OriginByte = Convert.ToByte(textBox3.Text);
                    if (Convert.ToInt16(textBox5.Text) > 255 || Convert.ToInt16(textBox5.Text) < 0)
                    {
                        MessageBox.Show("Данные введены некорректно", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    else
                        FakeByte = Convert.ToByte(textBox5.Text);
                }
                else
                {
                    MessageBox.Show("Перед считывынием информации введите значения для OriginByte и FakeByte", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            


            if (OriginBR != null)
            {
                string message = process_gif_stream(OriginBR, stegano_methode);
                richTextBox2.Text = message;
            }

            

            if (OriginBR != null)
            {
                OriginBR.Close();
                OriginBR = null;
            }
            try
            {
                ActiveImage = Image.FromFile(OriginFile.Name);
                pictureBox1.Image = NoImage;
                pictureBox2.Image = ActiveImage;
            }
            catch (IOException)
            {
                return;
            }
            if (OriginFile != null)
            {
                OriginFile.Close();
                OriginFile = null;
            }
            if (MessageBR != null)
            {
                MessageBR.Close();
                MessageBR = null;
            }
            if (MessageFile != null)
            {
                MessageFile.Close();
                MessageFile = null;
            }
            if (NewFile != null)
            {
                NewFile.Close();
                NewFile = null;
            }
            if (NewBR != null)
            {
                NewBR.Close();
                NewBR = null;
            }

            textBox4.Text = "";
        }

        private void button6_Click(object sender, EventArgs e)
        {

            //NoImage = Image.FromFile("noimage.png");
            pictureBox1.Image = NoImage;
            pictureBox2.Image = NoImage;
            if (ActiveImage != null)
                ActiveImage.Dispose();

            NewFile = null;
            OriginFile = null;
            string FilePic;
            //процедура открытия изображения
            OpenFileDialog dPic = new OpenFileDialog();
            //допустимые расширения изображения
            dPic.Filter = "Файлы изображений (*.gif)|*.gif|Все файлы (*.*)|*.*";
            if (dPic.ShowDialog() == DialogResult.OK)
            {
                //если изображение успешно открыто => запоминаем путь к файлу
                FilePic = dPic.FileName;
            }
            else
            {
                FilePic = "";
                return;
            }
            //создаем поток для работы с файлами
            try
            {
                //открываем поток
                OriginFile = new FileStream(FilePic, FileMode.Open);
            }
            catch (IOException)
            {
                MessageBox.Show("Ошибка открытия файла", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            textBox4.Text = FilePic;
            OriginBR = new BinaryReader(OriginFile);

        }

        private void textBox3_KeyPress(object sender, KeyPressEventArgs e)
        {

        }

        private void textBox5_KeyPress(object sender, KeyPressEventArgs e)
        {

        }









        static bool process_image_descriptor(BinaryReader gif_file,
                                 rgb[] gct,
                                 List<double> DATA)
        {
            image_descriptor_t image_descriptor;
            bool disposition;
            int compressed_data_length;
            List<byte> compressed_data = new List<byte>();
            byte lzw_code_size;
            int uncompressed_data_length = 0;
            byte[] uncompressed_data;

            image_descriptor.left_position = Convert.ToChar(OriginBR.ReadByte() | OriginBR.ReadByte() << 8);
            image_descriptor.top_position = Convert.ToChar(OriginBR.ReadByte() | OriginBR.ReadByte() << 8);
            image_descriptor.width = Convert.ToChar(OriginBR.ReadByte() | OriginBR.ReadByte() << 8);
            image_descriptor.height = Convert.ToChar(OriginBR.ReadByte() | OriginBR.ReadByte() << 8);
            image_descriptor.fields = OriginBR.ReadByte();

            ushort local_color_table_size = Convert.ToChar(1 << ((image_descriptor.fields & 0x07) + 1));

            // TODO if LCT = true, read the LCT
            rgb[] local_color_table = new rgb[local_color_table_size];
            if (Convert.ToBoolean(image_descriptor.fields) && local_color_table_size > 0)
            {
                for (int i = 0; i < local_color_table_size; i++) //local_color_table_size
                {
                    local_color_table[i].R = OriginBR.ReadByte();
                    //NewFile.WriteByte(local_color_table[i].R);
                    local_color_table[i].G = OriginBR.ReadByte();
                    //NewFile.WriteByte(local_color_table[i].G);
                    local_color_table[i].B = OriginBR.ReadByte();
                    //NewFile.WriteByte(local_color_table[i].B);
                }
            }

            disposition = true;

            lzw_code_size = OriginBR.ReadByte();
            

            compressed_data_length = read_sub_blocks(gif_file, compressed_data);

           
                uncompressed_data_length = image_descriptor.width *
                                            image_descriptor.height;
                uncompressed_data = new byte[uncompressed_data_length];

                uncompress(lzw_code_size, compressed_data, compressed_data_length,
                  uncompressed_data);
            if (Convert.ToBoolean(image_descriptor.fields))
                StegoAnalysis(uncompressed_data, local_color_table, DATA);
            else
                StegoAnalysis(uncompressed_data, gct, DATA);
            return disposition;
        }

        public static bool process_gif_stream(BinaryReader gif_file, List<double> DATA)
        {
            count_of_pictures = 0;
            List<byte> message = new List<byte>();
            byte[] header = OriginBR.ReadBytes(6);
            screen_descriptor_t screen_descriptor;
            int color_resolution_bits;
            int global_color_table_size = 0;  // number of entries in global_color_table
            rgb[] global_color_table = null;

            byte block_type = 0x0;
            if (Convert.ToBoolean(String.Compare("GIF89a", Encoding.UTF8.GetString(header))))
            {
                MessageBox.Show("Неверный формат изображения!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            screen_descriptor.width = Convert.ToChar(OriginBR.ReadByte() | OriginBR.ReadByte() << 8);

            screen_descriptor.height = Convert.ToChar(OriginBR.ReadByte() | OriginBR.ReadByte() << 8);

            screen_descriptor.fields = OriginBR.ReadByte();

            screen_descriptor.background_color = OriginBR.ReadByte();

            screen_descriptor.ratio = OriginBR.ReadByte();

            color_resolution_bits = ((screen_descriptor.fields & 0x70) >> 4) + 1;

            if (Convert.ToBoolean(screen_descriptor.fields >> 7))
            {
                global_color_table_size = 1 <<
                  (((screen_descriptor.fields & 0x07) + 1));

                global_color_table = new rgb[global_color_table_size];
                for (int i = 0; i < global_color_table_size; i++) //local_color_table_size
                {
                    global_color_table[i].R = OriginBR.ReadByte();

                    global_color_table[i].G = OriginBR.ReadByte();

                    global_color_table[i].B = OriginBR.ReadByte();

                }

            }
            int message_size = 65535;
            while (block_type != Constants.TRAILER)
            {
                block_type = OriginBR.ReadByte();
                switch (block_type)
                {
                    case Constants.IMAGE_DESCRIPTOR:
                        if (!process_image_descriptor(gif_file,
                                global_color_table,
                               DATA)){
                            return false;
                        }
                        count_of_pictures++;
                        //return true;
                        break;
                    case Constants.EXTENSION_INTRODUCER:
                        if (!process_extension(gif_file, false))
                        {
                            return false;
                        }
                        break;
                    case Constants.TRAILER:
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }

        private void button5_Click(object sender, EventArgs e)
        {
            List<double> plot_graph = new List<double>();

            if (OriginBR != null)
            {
                this.chart1.Series[0].Points.Clear();
                process_gif_stream(OriginBR,  plot_graph);
                double h = 100.0/(plot_graph.Count);
                //double h = 100.0 / (plot_graph.Count );
                double x = 0;
                for(int i = 0; i<plot_graph.Count; i++)
                //for(int i = 0; i<plot_graph.Count; i++)
                {
                    if (double.IsNegativeInfinity(plot_graph[i]))
                        this.chart1.Series[0].Points.AddXY(x, 0);
                    else
                    {
                        if (double.IsNaN(plot_graph[i]))
                            this.chart1.Series[0].Points.AddXY(x, 0);
                        else
                        {
                            if (plot_graph[i] > 100)
                                this.chart1.Series[0].Points.AddXY(x, 100);
                            else
                                this.chart1.Series[0].Points.AddXY(x, plot_graph[i]);
                        }
                    }
                    x += h;
                }
                textBox8.Text = "";
            }


            if (OriginBR != null)
            {
                OriginBR.Close();
                OriginBR = null;
            }
            if (OriginFile != null)
            {
                OriginFile.Close();
                OriginFile = null;
            }
            if (MessageBR != null)
            {
                MessageBR.Close();
                MessageBR = null;
            }
            if (MessageFile != null)
            {
                MessageFile.Close();
                MessageFile = null;
            }
            if (NewFile != null)
            {
                NewFile.Close();
                NewFile = null;
            }
            if (NewBR != null)
            {
                NewBR.Close();
                NewBR = null;
            }
        }

        private void button7_Click(object sender, EventArgs e)
        {
            pictureBox2.Image = NoImage;
            pictureBox1.Image = NoImage;
            if (ActiveImage != null)
                ActiveImage.Dispose();
            NewFile = null;
            string FilePic;
            //процедура открытия изображения
            OpenFileDialog dPic = new OpenFileDialog();
            //допустимые расширения изображения
            dPic.Filter = "Файлы изображений (*.gif)|*.gif|Все файлы (*.*)|*.*";
            if (dPic.ShowDialog() == DialogResult.OK)
            {
                //если изображение успешно открыто => запоминаем путь к файлу
                FilePic = dPic.FileName;
            }
            else
            {
                FilePic = "";
                return;
            }
            //создаем поток для работы с файлами
            try
            {
                //открываем поток
                OriginFile = new FileStream(FilePic, FileMode.Open);
            }
            catch (IOException)
            {
                MessageBox.Show("Ошибка открытия файла", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            textBox8.Text = FilePic;
            OriginBR = new BinaryReader(OriginFile);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            NoImage = Image.FromFile("noimage.png");
        }
    }
}
