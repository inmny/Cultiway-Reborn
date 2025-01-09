# 出生的人中有10%的概率会产生灵根，灵根的最大寿命是170岁，普通人的最大寿命是70岁，基础人口是1000人，模拟10000年，每年输出一次人口数据

import random

class Person:
    def __init__(self, age, is_rooted):
        self.age = age
        self.is_rooted = is_rooted

    def grow(self):
        self.age += 1
        pass
    
    def is_dead(self):
        if self.is_rooted:
            return self.age >= 170
        else:
            return self.age >= 70


class Simulator:
    def __init__(self, population=1000):
        self.population = []
        self.pop_count = population
        for i in range(self.pop_count):
            self.population.append(Person(0, random.random() < 0.1))

    def simulate(self):
        for i in range(10000):
            rooted = 0
            unrooted = 0
            for person in self.population:
                person.grow()
                if person.is_dead():
                    continue
                if person.is_rooted:
                    rooted += 1
                else:
                    unrooted += 1
            
            for j in range(len(self.population)):
                if self.population[j].is_dead():
                    self.population[j] = Person(0, random.random() < 0.1)
            
            if i % 100 == 0:
                print("Year %d, rooted: %d, unrooted: %d" % (i, rooted, unrooted))
            
if __name__ == "__main__":
    simulator = Simulator(10000)
    simulator.simulate()