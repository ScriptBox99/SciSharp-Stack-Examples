﻿/*****************************************************************************
   Copyright 2018 The TensorFlow.NET Authors. All Rights Reserved.

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
******************************************************************************/

using System.Diagnostics;
using Tensorflow;
using static Tensorflow.Binding;

namespace TensorFlowNET.Examples
{
    /// <summary>
    /// Neural Network classifier for Hand Written Digits
    /// Sample Neural Network architecture with two layers implemented for classifying MNIST digits.
    /// Use Stochastic Gradient Descent (SGD) optimizer. 
    /// http://www.easy-tf.com/tf-tutorials/neural-networks
    /// </summary>
    public class DigitRecognitionNN : SciSharpExample, IExample
    {
        const int img_h = 28;
        const int img_w = 28;
        int img_size_flat = img_h * img_w; // 784, the total number of pixels
        int n_classes = 10; // Number of classes, one class per digit
        // Hyper-parameters
        int epochs = 10;
        int batch_size = 100;
        float learning_rate = 0.001f;
        int h1 = 200; // number of nodes in the 1st hidden layer
        Datasets<MnistDataSet> mnist;

        Tensor x, y;
        Tensor loss, accuracy;
        Operation optimizer;

        int display_freq = 100;
        float accuracy_test = 0f;
        float loss_test = 1f;
        Session sess;

        public ExampleConfig InitConfig()
            => Config = new ExampleConfig
            {
                Name = "Digits Recognition Neural Network",
                Enabled = false,
                IsImportingGraph = false
            };

        public bool Run()
        {
            tf.compat.v1.disable_eager_execution();

            PrepareData();
            BuildGraph();

            sess = tf.Session();

            Train();
            Test();

            return loss_test < 0.09 && accuracy_test > 0.95;
        }

        public override Graph BuildGraph()
        {
            var graph = new Graph().as_default();

            // Placeholders for inputs (x) and outputs(y)
            x = tf.placeholder(tf.float32, shape: (-1, img_size_flat), name: "X");
            y = tf.placeholder(tf.float32, shape: (-1, n_classes), name: "Y");

            // Create a fully-connected layer with h1 nodes as hidden layer
            var fc1 = fc_layer(x, h1, "FC1", use_relu: true);
            // Create a fully-connected layer with n_classes nodes as output layer
            var output_logits = fc_layer(fc1, n_classes, "OUT", use_relu: false);
            // Define the loss function, optimizer, and accuracy
            var logits = tf.nn.softmax_cross_entropy_with_logits(labels: y, logits: output_logits);
            loss = tf.reduce_mean(logits, name: "loss");
            optimizer = tf.train.AdamOptimizer(learning_rate: learning_rate, name: "Adam-op").minimize(loss);
            var correct_prediction = tf.equal(tf.math.argmax(output_logits, 1), tf.math.argmax(y, 1), name: "correct_pred");
            accuracy = tf.reduce_mean(tf.cast(correct_prediction, tf.float32), name: "accuracy");

            // Network predictions
            var cls_prediction = tf.math.argmax(output_logits, axis: 1, name: "predictions");

            return graph;
        }

        private Tensor fc_layer(Tensor x, int num_units, string name, bool use_relu = true)
        {
            var in_dim = x.shape[1];

            var initer = tf.truncated_normal_initializer(stddev: 0.01f);
            var W = tf.compat.v1.get_variable("W_" + name,
                        dtype: tf.float32,
                        shape: (in_dim, num_units),
                        initializer: initer);

            var initial = tf.constant(0f, shape: num_units);
            var b = tf.compat.v1.get_variable("b_" + name,
                        dtype: tf.float32,
                        initializer: initial);

            var layer = tf.matmul(x, W.AsTensor()) + b.AsTensor();
            if (use_relu)
                layer = tf.nn.relu(layer);

            return layer;
        }

        public override void PrepareData()
        {
            var loader = new MnistModelLoader();
            mnist = loader.LoadAsync(".resources/mnist", oneHot: true, showProgressInConsole: true).Result;
        }

        public override void Train()
        {
            // Number of training iterations in each epoch
            var num_tr_iter = (int)mnist.Train.Labels.shape[0] / batch_size;

            var init = tf.global_variables_initializer();
            sess.run(init);

            float loss_val = 100.0f;
            float accuracy_val = 0f;

            var sw = new Stopwatch();
            sw.Start();

            foreach (var epoch in range(epochs))
            {
                print($"Training epoch: {epoch + 1}");
                // Randomly shuffle the training data at the beginning of each epoch 
                var (x_train, y_train) = mnist.Randomize(mnist.Train.Data, mnist.Train.Labels);

                foreach (var iteration in range(num_tr_iter))
                {
                    var start = iteration * batch_size;
                    var end = (iteration + 1) * batch_size;
                    var (x_batch, y_batch) = mnist.GetNextBatch(x_train, y_train, start, end);

                    // Run optimization op (backprop)
                    sess.run(optimizer, (x, x_batch), (y, y_batch));

                    if (iteration % display_freq == 0)
                    {
                        // Calculate and display the batch loss and accuracy
                        (loss_val, accuracy_val) = sess.run((loss, accuracy), (x, x_batch), (y, y_batch));
                        print($"iter {iteration.ToString("000")}: Loss={loss_val.ToString("0.0000")}, Training Accuracy={accuracy_val.ToString("P")} {sw.ElapsedMilliseconds}ms");
                        sw.Restart();
                    }
                }

                // Run validation after every epoch
                (loss_val, accuracy_val) = sess.run((loss, accuracy), (x, mnist.Validation.Data), (y, mnist.Validation.Labels));
                print("---------------------------------------------------------");
                print($"Epoch: {epoch + 1}, validation loss: {loss_val.ToString("0.0000")}, validation accuracy: {accuracy_val.ToString("P")}");
                print("---------------------------------------------------------");
            }

        }

        public override void Test()
        {
            (loss_test, accuracy_test) = sess.run((loss, accuracy), (x, mnist.Test.Data), (y, mnist.Test.Labels));
            print("---------------------------------------------------------");
            print($"Test loss: {loss_test.ToString("0.0000")}, test accuracy: {accuracy_test.ToString("P")}");
            print("---------------------------------------------------------");
        }
    }
}
