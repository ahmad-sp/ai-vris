from django.db import models

class InterviewSession(models.Model):
    candidate_name = models.CharField(max_length=100)
    role = models.CharField(max_length=100)
    current_step = models.CharField(max_length=50, default="Role Selection")
    created_at = models.DateTimeField(auto_now_add=True)
    completed = models.BooleanField(default=False)

    def __str__(self):
        return f"{self.candidate_name} - {self.role}"


class InterviewResponse(models.Model):
    session = models.ForeignKey(InterviewSession, on_delete=models.CASCADE, related_name="responses")
    step = models.CharField(max_length=50)
    question = models.TextField()
    answer = models.TextField(null=True, blank=True)
    score = models.IntegerField(default=0)
    created_at = models.DateTimeField(auto_now_add=True)

    def __str__(self):
        return f"{self.step} - {self.score}"
