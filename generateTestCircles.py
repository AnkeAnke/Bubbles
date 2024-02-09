# Generate circles in [0.1, 0.8]², radius 0.1 
import random

f = open('./testCircles.csv', 'w')

for circle in range(0, 1000):
    f.write(f'{random.random()*0.8 + 0.1},{random.random()*0.8 + 0.1},{random.random()* 0.1}\n')
f.close()
